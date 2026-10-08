using Microsoft.EntityFrameworkCore;
using OpportunityPilot.Application.Common;
using OpportunityPilot.Domain.Outreach;
using OpportunityPilot.Domain.Staffing;

namespace OpportunityPilot.Application.Staffing;

/// <summary>
/// Conversation (X5/X6) and meetings (X7) for a staffing deal. Outbound messages and meetings are approved for their exact
/// content, then sent by the user, who records the receipt. Replies are recorded with the user's own intent label; an
/// "Unsubscribe" reply adds the sender to the suppression list so no later draft to them can be approved.
/// </summary>
public sealed partial class StaffingPipelineService
{
    public async Task<StaffingConversationDto> ConversationAsync(Guid dealId, CancellationToken ct)
    {
        _ = await FindDealAsync(dealId, ct);
        var messages = await Owned(db.StaffingMessages).Where(m => m.DealId == dealId).OrderBy(m => m.CreatedAt).ToListAsync(ct);
        var suppressed = await SuppressedAsync(messages.Select(m => m.Counterpart), ct);
        var meetings = await Owned(db.StaffingMeetings).Where(m => m.DealId == dealId).OrderBy(m => m.StartsAt).ToListAsync(ct);
        return new(messages.Select(m => ToDto(m, suppressed.Contains(Normalize(m.Counterpart)))).ToList(), meetings.Select(ToDto).ToList());
    }

    public async Task<StaffingMessageDto> DraftMessageAsync(Guid dealId, DraftMessageRequest r, CancellationToken ct)
    {
        if (r is null) throw Invalid("body", "Request body is required.");
        var deal = await FindDealAsync(dealId, ct);
        await EnsureContactAsync(deal, r.ContactId, ct);
        await EnsureNotSuppressedAsync(r.Recipient, ct);
        StaffingMessage message;
        try { message = StaffingMessage.Draft(user.OwnerId, dealId, r.ContactId, r.Channel, r.Recipient, r.Subject, r.Body, Now); }
        catch (ArgumentException ex) { throw Invalid("message", ex.Message); }
        db.StaffingMessages.Add(message);
        Log(deal, StaffingDealActivityType.MessageChanged, $"{r.Channel} draft to {message.Counterpart} saved.");
        await SaveAsync(ct);
        return ToDto(message, false);
    }

    public Task<StaffingMessageDto> EditMessageAsync(Guid dealId, Guid id, EditMessageRequest r, CancellationToken ct) =>
        MessageActionAsync(dealId, id, r?.ExpectedVersion, async (deal, m) =>
        {
            await EnsureNotSuppressedAsync(r!.Recipient, ct);
            m.Edit(r.Recipient, r.Subject, r.Body, Now);
            Log(deal, StaffingDealActivityType.MessageChanged, $"{m.Channel} draft to {m.Counterpart} edited; approval cleared.");
        }, ct);

    public Task<StaffingMessageDto> ApproveMessageAsync(Guid dealId, Guid id, VersionRequest r, CancellationToken ct) =>
        MessageActionAsync(dealId, id, null, async (deal, m) =>
        {
            await EnsureNotSuppressedAsync(m.Counterpart, ct);
            m.Approve(r?.ExpectedVersion ?? -1, Now);
            Log(deal, StaffingDealActivityType.MessageChanged, $"{m.Channel} message to {m.Counterpart} approved (version {m.Version}).");
            Advance(deal, StaffingDealStage.OutreachApproved);
        }, ct);

    public Task<StaffingMessageDto> MarkMessageSentAsync(Guid dealId, Guid id, MessageSentRequest r, CancellationToken ct) =>
        MessageActionAsync(dealId, id, null, async (deal, m) =>
        {
            if (r is null) throw Invalid("body", "Request body is required.");
            await EnsureNotSuppressedAsync(m.Counterpart, ct);
            m.MarkSent(r.ExpectedVersion, r.Receipt, Now);
            Log(deal, StaffingDealActivityType.ManualActionConfirmed, $"{m.Channel} message sent to {m.Counterpart} by the user: {m.Receipt}");
            Advance(deal, StaffingDealStage.Contacted);
        }, ct);

    public async Task<StaffingMessageDto> RecordReplyAsync(Guid dealId, RecordReplyRequest r, CancellationToken ct)
    {
        if (r is null) throw Invalid("body", "Request body is required.");
        var deal = await FindDealAsync(dealId, ct);
        await EnsureContactAsync(deal, r.ContactId, ct);
        var receivedAt = r.ReceivedAt is { } at ? at.ToUniversalTime() : Now;
        if (receivedAt > Now.AddMinutes(5)) throw Invalid("receivedAt", "A reply cannot be received in the future.");
        StaffingMessage message;
        try { message = StaffingMessage.Received(user.OwnerId, dealId, r.ContactId, r.Channel, r.From, r.Subject, r.Body, r.Intent, receivedAt, Now); }
        catch (ArgumentException ex) { throw Invalid("message", ex.Message); }
        db.StaffingMessages.Add(message);
        Log(deal, StaffingDealActivityType.MessageChanged, $"Reply from {message.Counterpart} recorded ({r.Intent}).");
        Advance(deal, StaffingDealStage.Replied);
        await SuppressIfAskedAsync(deal, message, ct);
        await SaveAsync(ct);
        return ToDto(message, message.Intent == ReplyIntent.Unsubscribe);
    }

    public Task<StaffingMessageDto> ClassifyReplyAsync(Guid dealId, Guid id, ClassifyReplyRequest r, CancellationToken ct) =>
        MessageActionAsync(dealId, id, r?.ExpectedVersion, async (deal, m) =>
        {
            m.Classify(r!.Intent, Now);
            Log(deal, StaffingDealActivityType.MessageChanged, $"Reply from {m.Counterpart} marked {r.Intent}.");
            await SuppressIfAskedAsync(deal, m, ct);
        }, ct);

    public async Task<StaffingMeetingDto> PlanMeetingAsync(Guid dealId, Guid? id, PlanMeetingRequest r, CancellationToken ct)
    {
        if (r is null) throw Invalid("body", "Request body is required.");
        if (r.StartsAt.Kind == DateTimeKind.Unspecified) throw Invalid("startsAt", "Send the time in UTC (e.g. 2026-10-12T09:30:00Z).");
        if (!ValidTimeZone(r.TimeZone)) throw Invalid("timeZone", "Use an IANA time zone, e.g. Asia/Kolkata.");
        var deal = await FindDealAsync(dealId, ct);
        StaffingMeeting meeting;
        if (id is { } existing)
        {
            meeting = await FindAsync(db.StaffingMeetings, existing, "Meeting", ct);
            if (meeting.DealId != dealId) throw new NotFoundException("Meeting not found.");
            if (r.ExpectedVersion != meeting.Version) throw Stale("meeting", meeting.Version);
        }
        else
        {
            meeting = new StaffingMeeting(user.OwnerId, dealId, Now);
            db.StaffingMeetings.Add(meeting);
        }
        var wasInvited = meeting.State == MeetingState.Invited;
        try { meeting.Plan(r.Title, r.StartsAt.ToUniversalTime(), r.TimeZone, r.DurationMinutes, r.Invitees, r.Agenda, r.Link, Now); }
        catch (ArgumentException ex) { throw Invalid("meeting", ex.Message); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        Log(deal, StaffingDealActivityType.MeetingChanged, wasInvited
            ? $"Meeting \"{meeting.Title}\" rescheduled to {meeting.StartsAt:yyyy-MM-dd HH:mm} UTC; approve and send a new invite."
            : $"Meeting \"{meeting.Title}\" proposed for {meeting.StartsAt:yyyy-MM-dd HH:mm} UTC ({meeting.TimeZone}).");
        await SaveAsync(ct);
        return ToDto(meeting);
    }

    public Task<StaffingMeetingDto> ApproveMeetingAsync(Guid dealId, Guid id, VersionRequest r, CancellationToken ct) =>
        MeetingActionAsync(dealId, id, (deal, m) =>
        {
            m.Approve(r?.ExpectedVersion ?? -1, Now);
            Log(deal, StaffingDealActivityType.MeetingChanged, $"Meeting \"{m.Title}\" approved.");
        }, ct);

    public Task<StaffingMeetingDto> MarkMeetingInvitedAsync(Guid dealId, Guid id, MeetingInvitedRequest r, CancellationToken ct) =>
        MeetingActionAsync(dealId, id, (deal, m) =>
        {
            if (r is null) throw Invalid("body", "Request body is required.");
            m.MarkInvited(r.ExpectedVersion, r.Receipt, Now);
            Log(deal, StaffingDealActivityType.ManualActionConfirmed, $"Invite for \"{m.Title}\" sent by the user: {m.Receipt}");
            Advance(deal, StaffingDealStage.MeetingScheduled);
        }, ct);

    public Task<StaffingMeetingDto> FinishMeetingAsync(Guid dealId, Guid id, FinishMeetingRequest r, CancellationToken ct) =>
        MeetingActionAsync(dealId, id, (deal, m) =>
        {
            if (r is null || r.ExpectedVersion != m.Version) throw Stale("meeting", m.Version);
            m.Finish(r.Held, r.Outcome, Now);
            Log(deal, StaffingDealActivityType.MeetingChanged, $"Meeting \"{m.Title}\" {(r.Held ? "held" : "cancelled")}{(m.Outcome is null ? "" : $": {m.Outcome}")}.");
        }, ct);

    private async Task<StaffingMessageDto> MessageActionAsync(Guid dealId, Guid id, int? expectedVersion,
        Func<StaffingDeal, StaffingMessage, Task> act, CancellationToken ct)
    {
        var deal = await FindDealAsync(dealId, ct);
        var message = await FindAsync(db.StaffingMessages, id, "Message", ct);
        if (message.DealId != dealId) throw new NotFoundException("Message not found.");
        if (expectedVersion is { } v && v != message.Version) throw Stale("message", message.Version);
        try { await act(deal, message); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        catch (ArgumentException ex) { throw Invalid("message", ex.Message); }
        await SaveAsync(ct);
        return ToDto(message, (await SuppressedAsync([message.Counterpart], ct)).Count > 0);
    }

    private async Task<StaffingMeetingDto> MeetingActionAsync(Guid dealId, Guid id, Action<StaffingDeal, StaffingMeeting> act, CancellationToken ct)
    {
        var deal = await FindDealAsync(dealId, ct);
        var meeting = await FindAsync(db.StaffingMeetings, id, "Meeting", ct);
        if (meeting.DealId != dealId) throw new NotFoundException("Meeting not found.");
        try { act(deal, meeting); }
        catch (InvalidOperationException ex) { throw new ConflictException(ex.Message); }
        catch (ArgumentException ex) { throw Invalid("meeting", ex.Message); }
        await SaveAsync(ct);
        return ToDto(meeting);
    }

    private async Task SuppressIfAskedAsync(StaffingDeal deal, StaffingMessage reply, CancellationToken ct)
    {
        if (reply.Intent != ReplyIntent.Unsubscribe) return;
        var normalized = Normalize(reply.Counterpart);
        if (await db.Suppressions.AnyAsync(s => s.OwnerId == user.OwnerId && s.NormalizedRecipient == normalized, ct)) return;
        db.Suppressions.Add(new Suppression(user.OwnerId, reply.Counterpart, "Asked not to be contacted (staffing reply)", Now));
        Log(deal, StaffingDealActivityType.MessageChanged, $"{reply.Counterpart} added to the suppression list at their request.");
    }

    private async Task EnsureNotSuppressedAsync(string? recipient, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return;
        var normalized = Normalize(recipient);
        if (await db.Suppressions.AnyAsync(s => s.OwnerId == user.OwnerId && s.NormalizedRecipient == normalized, ct))
            throw Invalid("recipient", "This recipient is on your suppression list (they asked not to be contacted).");
    }

    private async Task<HashSet<string>> SuppressedAsync(IEnumerable<string> counterparts, CancellationToken ct)
    {
        var keys = counterparts.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Normalize).Distinct().ToList();
        return (await db.Suppressions.Where(s => s.OwnerId == user.OwnerId && keys.Contains(s.NormalizedRecipient))
            .Select(s => s.NormalizedRecipient).ToListAsync(ct)).ToHashSet();
    }

    private async Task EnsureContactAsync(StaffingDeal deal, Guid? contactId, CancellationToken ct)
    {
        if (contactId is { } id && !await db.StaffingContacts.AnyAsync(c => c.Id == id && c.OwnerId == user.OwnerId && c.AccountId == deal.AccountId, ct))
            throw new NotFoundException("Contact not found on this deal's client.");
    }

    private static string Normalize(string value) => Suppression.Normalize(value);

    private static StaffingMessageDto ToDto(StaffingMessage m, bool suppressed) =>
        new(m.Id, m.DealId, m.ContactId, m.Direction, m.Channel, m.Counterpart, m.Subject, m.Body, m.State, m.Receipt,
            m.OccurredAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null, m.Intent, suppressed, m.Version, m.CreatedAt);

    private static StaffingMeetingDto ToDto(StaffingMeeting m) =>
        new(m.Id, m.DealId, m.Title, DateTime.SpecifyKind(m.StartsAt, DateTimeKind.Utc), m.TimeZone, m.DurationMinutes, m.Invitees,
            m.Agenda, m.Link, m.State, m.Receipt, m.Outcome, m.Version, m.UpdatedAt);
}
