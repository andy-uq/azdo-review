using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzdoPr.Models;

namespace AzdoPr;

public sealed class AzdoClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _org;
    private readonly string _project;
    private readonly string _repo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AzdoClient(string pat, string org, string project, string repo)
    {
        CheckPatExpiry();

        _org = org;
        _project = project;
        _repo = repo;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static void CheckPatExpiry()
    {
        var expiryStr = Environment.GetEnvironmentVariable("AZDO_PAT_EXPIRES");
        if (expiryStr is null) return;
        if (!DateOnly.TryParse(expiryStr, out var expiry)) return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today > expiry)
            throw new InvalidOperationException(
                $"AZDO_PAT expired on {expiry:yyyy-MM-dd}. Generate a new PAT and update AZDO_PAT.");
        if (today >= expiry.AddDays(-7))
            Console.Error.WriteLine($"Warning: AZDO_PAT expires on {expiry:yyyy-MM-dd} ({expiry.DayNumber - today.DayNumber} days remaining)");
    }

    private string BaseUrl =>
        $"https://dev.azure.com/{_org}/{_project}/_apis/git/repositories/{_repo}";

    public async Task<PrInfo> GetPullRequestAsync(int prId)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}?api-version=7.1";
        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<PrInfo>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize PR info");
    }

    public async Task<List<PrThread>> GetThreadsAsync(int prId)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/threads?api-version=7.1";
        var json = await GetStringAsync(url);
        var wrapper = JsonSerializer.Deserialize<ValueWrapper<PrThread>>(json, JsonOptions);
        return wrapper?.Value ?? [];
    }

    public async Task<string> GetFileContentAsync(string path, string version)
    {
        // path must not be double-encoded; AzDO expects it as a query param value
        var url = $"{BaseUrl}/items?path={Uri.EscapeDataString(path)}&versionDescriptor.version={Uri.EscapeDataString(version)}&versionDescriptor.versionType=branch&api-version=7.1&$format=text";
        return await GetStringAsync(url);
    }

    public async Task ResolveThreadAsync(int prId, int threadId)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/threads/{threadId}?api-version=7.1";
        var body = JsonSerializer.Serialize(new { status = "fixed" });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        await SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = content });
    }

    public async Task PostReplyAsync(int prId, int threadId, string message)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/threads/{threadId}/comments?api-version=7.1";
        var body = JsonSerializer.Serialize(new { content = message, commentType = 1 });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = content });
    }

    public async Task<int> CreateThreadAsync(int prId, string message, string? filePath = null, int? line = null)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/threads?api-version=7.1";

        object? threadContext = null;
        if (filePath is not null)
        {
            threadContext = line.HasValue
                ? new
                {
                    filePath,
                    rightFileStart = new { line = line.Value, offset = 1 },
                    rightFileEnd = new { line = line.Value, offset = 1 }
                }
                : (object)new { filePath };
        }

        var payload = new
        {
            comments = new[] { new { content = message, commentType = 1 } },
            status = "active",
            threadContext
        };

        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = content });
        await EnsureSuccessOrThrowHelpful(response);

        var responseJson = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<PrThread>(responseJson, JsonOptions);
        return created?.Id ?? 0;
    }

    public async Task UpdateThreadStatusAsync(int prId, int threadId, string status)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/threads/{threadId}?api-version=7.1";
        var body = JsonSerializer.Serialize(new { status });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        await SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = content });
    }

    private async Task<string> GetStringAsync(string url)
    {
        var response = await _http.GetAsync(url);
        await EnsureSuccessOrThrowHelpful(response);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task SendAsync(HttpRequestMessage request)
    {
        var response = await _http.SendAsync(request);
        await EnsureSuccessOrThrowHelpful(response);
    }

    private async Task EnsureSuccessOrThrowHelpful(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(
                $"401 Unauthorized - AZDO_PAT may be expired or invalid. Generate a new PAT at https://dev.azure.com/{_org}/_usersSettings/tokens");
        if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            throw new InvalidOperationException(
                "203 Non-Authoritative - AZDO_PAT may be expired. Generate a new PAT.");
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"AzDO API returned {(int)response.StatusCode}: {body}");
    }

    public async Task<List<PrIteration>> GetIterationsAsync(int prId)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/iterations?api-version=7.1";
        var json = await GetStringAsync(url);
        var wrapper = JsonSerializer.Deserialize<ValueWrapper<PrIteration>>(json, JsonOptions);
        return wrapper?.Value ?? [];
    }

    public async Task<List<IterationChange>> GetIterationChangesAsync(int prId, int iterationId, int compareTo = 0)
    {
        var url = $"{BaseUrl}/pullRequests/{prId}/iterations/{iterationId}/changes?api-version=7.1&compareTo={compareTo}&$top=1000";
        var json = await GetStringAsync(url);
        var result = JsonSerializer.Deserialize<IterationChanges>(json, JsonOptions);
        return result?.ChangeEntries ?? [];
    }

    public void Dispose() => _http.Dispose();

    private sealed class ValueWrapper<T>
    {
        public List<T> Value { get; set; } = [];
    }
}
