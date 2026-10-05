using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpportunityPilot.IntegrationTests;

/// <summary>Small helpers over the research endpoints. Responses are read as JSON so the wire shape itself is tested.</summary>
internal static class ResearchApi
{
    public static async Task<JsonElement> Json(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<JsonElement> GetJson(this HttpClient client, string url) =>
        await (await client.GetAsync(url)).Json(HttpStatusCode.OK);

    public static async Task<Guid> CreateProfileAsync(HttpClient client, string type = "Candidate")
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/profiles",
            new { type, name = "Research profile", data = new { summary = "Fictional test profile" }, confirmed = true })).Json(HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> CreateCampaignAsync(HttpClient client, string mode, object criteria, string name = "Campaign", object? weights = null,
        int? resultLimit = null, int? autoSuggestMinScore = null)
    {
        var profileId = await CreateProfileAsync(client, mode == "Job" ? "Candidate" : "Product");
        return await (await client.PostAsJsonAsync("/api/v1/campaigns",
            new { profileId, mode, name, goal = "Find good matches", criteria, weights, resultLimit, autoSuggestMinScore })).Json(HttpStatusCode.Created);
    }

    public static async Task<JsonElement> AddBoardAsync(HttpClient client, Guid campaignId, string kind, string? url = null) =>
        await (await client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/sources", new { kind, url })).Json(HttpStatusCode.Created);

    public static async Task<JsonElement> AddPasteAsync(HttpClient client, Guid campaignId, string text, string? label = null) =>
        await (await client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/sources", new { kind = "Paste", text, label }))
            .Json(HttpStatusCode.Created);

    public static async Task<JsonElement> AddUrlAsync(HttpClient client, Guid campaignId, string kind, string url) =>
        await (await client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/sources", new { kind, url, permissionNote = "Public test page" }))
            .Json(HttpStatusCode.Created);

    public static async Task<Guid> QueueAsync(HttpClient client, Guid campaignId) =>
        (await (await client.PostAsync($"/api/v1/campaigns/{campaignId}/research", null)).Json(HttpStatusCode.Accepted))
        .GetProperty("jobId").GetGuid();

    public static async Task<JsonElement> JobAsync(HttpClient client, Guid jobId) =>
        await client.GetJson($"/api/v1/research-jobs/{jobId}");

    public static async Task<List<JsonElement>> OpportunitiesAsync(HttpClient client, Guid campaignId, string query = "")
    {
        var page = await client.GetJson($"/api/v1/campaigns/{campaignId}/opportunities{query}");
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    public static Guid Id(this JsonElement e) => e.GetProperty("id").GetGuid();

    public static string Str(this JsonElement e, string name) => e.GetProperty(name).GetString()!;

    public static int Int(this JsonElement e, string name) => e.GetProperty(name).GetInt32();
}
