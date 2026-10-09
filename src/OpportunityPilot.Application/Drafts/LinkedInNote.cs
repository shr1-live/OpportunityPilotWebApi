namespace OpportunityPilot.Application.Drafts;

/// <summary>A LinkedIn connection note: short, personal, always whole sentences, never longer than LinkedIn allows.</summary>
public static class LinkedInNote
{
    public const int MaxLength = 300;

    public static string Build(string organization, string title, string offer, string name)
    {
        var head = $"Hi, I saw {organization} is hiring for {title}. ";
        var tail = $" I'd like to connect. {name}";
        var room = MaxLength - head.Length - tail.Length;
        string middle;
        if (offer.StartsWith('[') || room < 20) middle = "";
        else if (offer.Length <= room) middle = offer.TrimEnd('.', ' ') + ".";
        else
        {
            var cut = offer[..(room - 1)];
            var space = cut.LastIndexOf(' ');
            middle = (space > 10 ? cut[..space] : cut).TrimEnd(',', ' ', '.') + "…";
        }
        var text = middle.Length == 0 ? head.TrimEnd() + tail : head + middle + tail;
        return text.Length <= MaxLength ? text : text[..(MaxLength - 1)].TrimEnd() + "…";
    }
}
