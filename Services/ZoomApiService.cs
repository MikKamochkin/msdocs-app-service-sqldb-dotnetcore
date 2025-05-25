using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCoreSqlDb.Services
{
    public class ZoomApiService : IZoomApiService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _accountId;

        private readonly IHttpClientFactory _factory;
        private readonly ILogger<ZoomApiService> _log;

        private readonly SemaphoreSlim _tokenGate = new(1, 1); //Initial count = 1, max count = 1;
        private string? _cachedToken;
        private DateTime _tokenExpiresUtc;

        //Allow at most 2 concurrent Zoom REST calls (token + any endpoint)
        //Used to prevent Zoom's 429 errors ("Too Many Requests")
        //Rest of the calls after the 2 limit are waiting for the slots to free up
        private static readonly SemaphoreSlim _throttle = new(initialCount: 2);

        public ZoomApiService(IHttpClientFactory factory,
                              ILogger<ZoomApiService> log,
                              IConfiguration config)
        {
            _factory = factory;
            _log = log;
            _clientId = config["ZoomClientId"] ?? throw new InvalidOperationException("ZoomClientId not configured");
            _clientSecret = config["ZoomClientSecret"] ?? throw new InvalidOperationException("ZoomClientSecret not configured");
            _accountId = config["ZoomAccountId"] ?? throw new InvalidOperationException("ZoomAccountId not configured");
        }

        public Task<string> GetAccessTokenAsync() => GetAccessTokenInternalAsync();

        //Not currently used, can be used for a view file to show all active meetings
        public async Task<ZoomParticipantViewModel> ListLiveMeetingParticipantsAsync(string meetingId)
        {
            await _throttle.WaitAsync();
            try
            {
                using var client = await BuildClientAsync();

                var resp = await client.GetAsync(
                    $"https://api.zoom.us/v2/metrics/meetings/{meetingId}/participants?type=live");

                int.TryParse(resp.Headers.TryGetValues("X-RateLimit-Remaining", out var vals)
                                ? vals.FirstOrDefault()
                                : null,
                            out int remaining);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new ZoomParticipantViewModel { RateLimitRemaining = remaining };

                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var list = new List<ZoomParticipant>();
                if (doc.RootElement.TryGetProperty("participants", out var arr))
                {
                    foreach (var p in arr.EnumerateArray())
                    {
                        list.Add(new ZoomParticipant
                        {
                            UserName = p.GetProperty("user_name").GetString() ?? string.Empty,
                            JoinTime = p.GetProperty("join_time").GetString() ?? string.Empty
                        });
                    }
                }

                return new ZoomParticipantViewModel
                {
                    Participants = list,
                    RateLimitRemaining = remaining
                };
            }
            finally
            {
                _throttle.Release();
            }
        }

        //Calls Zoom api to create a meeting for the host by Id
        //Return a ZoomCreateMeetingResult object with info about the meeting to be stored in ZoomMeetings table
        public async Task<ZoomCreateMeetingResult> CreateInstantMeetingAsync(string hostId, string topic = "Lesson")
        {
            await _throttle.WaitAsync();
            try
            {
                using var client = await BuildClientAsync();

                var payload = new
                {
                    topic,
                    type = 1,               // instant
                    settings = new
                    {
                        waiting_room = false,
                        join_before_host = true,
                        host_video = true,
                        participant_video = true
                    }
                };

                var resp = await client.PostAsJsonAsync(
                               $"https://api.zoom.us/v2/users/{hostId}/meetings",
                               payload);

                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();

                _log.LogInformation("Created instant meeting for {Host}.", hostId);
                _log.LogDebug("Zoom response: {Json}", json);

                var doc = JsonDocument.Parse(json).RootElement;

                return new ZoomCreateMeetingResult
                {
                    Id = doc.GetProperty("id").GetRawText(),
                    Uuid = doc.GetProperty("uuid").GetString()!,
                    JoinUrl = doc.GetProperty("join_url").GetString() ?? string.Empty,
                    StartUrl = doc.GetProperty("start_url").GetString() ?? string.Empty,
                    Passcode = doc.TryGetProperty("password", out var pw)
                                   ? pw.GetString() ?? string.Empty
                                   : string.Empty,
                    HostEmail = doc.GetProperty("host_email").GetString() ?? string.Empty
                };
            }
            finally
            {
                _throttle.Release();
            }
        }

        //Called from ZoomMeetingService
        //Gets uuid of the meeting with zoomId, and calls the end meeting api to end the meeting
        //Zoom returns uuids as a list for the requested user, hence the foreach loop, but for this it's only ever 1 uuid
        public async Task EndMeetingAsync(string zoomId)
        {
            var liveIds = await GetLiveMeetingUuidsAsync(zoomId);

            if (liveIds.Count == 0)
            {
                Console.WriteLine("Host has no live meetings.");
                return;
            }

            foreach (var id in liveIds)
            {
                await EndZoomMeetingAsync(id);
            }
        }

        //When ending a Zoom meeting, Zoom expects a uuid of a meeting (meeting id isn't specific enough, uuid is id of instance)
        //This gets a list of uuids for the zoomId (as of now, it's only 1 per host user, but still returns as a list)
        //This uuid is based back to EndZoomMeetingAsync(id) to end the meeting
        public async Task<List<string>> GetLiveMeetingUuidsAsync(string zoomId)
        {
            await _throttle.WaitAsync();
            try
            {
                using var client = await BuildClientAsync();

                var resp = await client.GetAsync(
                    $"https://api.zoom.us/v2/users/{zoomId}/meetings?type=live");

                resp.EnsureSuccessStatusCode();                     // 200 or 204

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

                var list = new List<string>();
                if (doc.RootElement.TryGetProperty("meetings", out var meetings))
                {
                    foreach (var m in meetings.EnumerateArray())
                    {
                        // ── numeric meeting number ───────────────────────────────
                        var idProp = m.GetProperty("id");
                        string meetingNumber = idProp.ValueKind == JsonValueKind.String
                                            ? idProp.GetString()!
                                            : idProp.GetInt64().ToString();

                        list.Add(meetingNumber);

                    }
                }
                return list;
            }
            finally
            {
                _throttle.Release();
            }
        }

        //Entry point from ZoomMeetingService
        //Calls Zoom api to end meeting with uuid as param
        public async Task EndZoomMeetingAsync(string meetingUuid)
        {
            await _throttle.WaitAsync();
            try
            {
                using var client = await BuildClientAsync();

                //encoded is the meetingUuid made safe to embed into url (percent-encoded / URL-encoded)
                string encoded = Uri.EscapeDataString(meetingUuid);
                Console.WriteLine($"→ PUT /meetings/{encoded}/status  (raw = {meetingUuid})");

                var body = new { action = "end" };
                var resp = await client.PutAsJsonAsync(
                    $"https://api.zoom.us/v2/meetings/{encoded}/status",
                    body);

                Console.WriteLine(resp.IsSuccessStatusCode
                    ? $"Meeting {meetingUuid} ended."
                    : $"Failed ({resp.StatusCode}) – {await resp.Content.ReadAsStringAsync()}");
            }
            finally
            {
                _throttle.Release();
            }

        }


        // ─── helpers ───────────────────────────────────────────────────────────────
        private async Task<HttpClient> BuildClientAsync()
        {
            var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", await GetAccessTokenInternalAsync());
            return c;
        }

        //When calling any Zoom REST endpoint, you must include a valid OAuth access token in the header/
        //GetAccessTokenInternalAsync gets that token
        //Zoom grants a short-lived JWT token for ~1hr.
        // Instead of getting a new token on every Zoom call, we skip getting a new token for most of the calls to improve performance
        //By checking the expiration of _cachedToken, we skip the calling Zoom's token endpoint while the token is valid for at least another 5 mins
        private async Task<string> GetAccessTokenInternalAsync()
        {
            //If a token already exists with an expiration > 5 mins from now, count is as valid and return it
            if (!string.IsNullOrEmpty(_cachedToken) &&
                _tokenExpiresUtc > DateTime.UtcNow.AddMinutes(5))
                return _cachedToken;

            //If multiple callers hit this at once, they queue here isntead of spamming Zoom with parallel token requests
            await _tokenGate.WaitAsync();
            try
            {
                //Another caller might have already fetched a fresh token while this one was waiting for _tokenGate
                //If so, return the valid token instead of getting a new one
                if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiresUtc > DateTime.UtcNow.AddMinutes(5))
                    return _cachedToken;

                //Build an HttpClient for the token request
                using var client = _factory.CreateClient();
                //Zoom uses HTTP basic autho for this endpoint: Authorization: Basic Base64(clientId:clientSecret)
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", basic);

                //Request a new token
                var resp = await client.PostAsync(
                               $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={_accountId}",
                               null);

                resp.EnsureSuccessStatusCode();

                //Parse JSON response
                var tokenJson = await resp.Content.ReadAsStringAsync();

                var tzResp = JsonSerializer.Deserialize<ZoomTokenResponse>(tokenJson)
                 ?? throw new Exception("Bad token JSON");

                _cachedToken = tzResp.AccessToken;
                _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(tzResp.ExpiresIn);

                //_log.LogDebug("Obtained Zoom token; expires {Utc}.", _tokenExpiresUtc);
                return _cachedToken;
            }
            finally
            {
                _tokenGate.Release();
            }
        }
    }

    // ─── DTOs ──────────────────────────────────────────────────────────────────────
    public sealed class ZoomTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    public sealed class ZoomParticipant
    {
        public string UserName { get; set; } = string.Empty;
        public string JoinTime { get; set; } = string.Empty;
    }

    public sealed class ZoomParticipantViewModel
    {
        public List<ZoomParticipant> Participants { get; set; } = new();
        public int RateLimitRemaining { get; set; }
    }

    public sealed class ZoomCreateMeetingResult
    {
        public string Id { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public string StartUrl { get; set; } = string.Empty;
        public string Passcode { get; set; } = string.Empty;
        public string HostEmail { get; set; } = string.Empty;
    }
}
