using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Services;

static class ApiService
{
    static readonly HttpClient _httpClient;
    static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    static ApiService()
    {
        var baseUrl = ConfigService.GetApiUrl();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public static async Task SendNoteAsync(NoteLocal note)
    {
        if (note == null) throw new ArgumentNullException(nameof(note));

        var payload = new
        {
            id = note.Id,
            title = string.IsNullOrWhiteSpace(note.Title) ? "Nota" : note.Title,
            content = string.IsNullOrWhiteSpace(note.Content) ? NoteDefaults.DefaultDocumentXaml : note.Content,
            updated_at = note.UpdatedAt,
            target_user_id = note.SourceUserId,
            created_by_user_id = note.CreatedByUserId,
            group_id = note.GroupId,
            deleted = note.Deleted
        };

        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("sync/send", content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"API retornou {(int)response.StatusCode}: {body}");
        }
    }

    public static async Task<List<RemoteNoteEvent>> GetUpdatesAsync(long since)
    {
        var response = await _httpClient.GetAsync($"sync/updates?since={since}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return new List<RemoteNoteEvent>();
        }

        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();
        var events = await JsonSerializer.DeserializeAsync<List<RemoteNoteEvent>>(stream, _jsonOptions);
        return events ?? new List<RemoteNoteEvent>();
    }

    public static async Task<bool> AckAsync(IReadOnlyCollection<string> eventIds)
    {
        if (eventIds == null || eventIds.Count == 0)
        {
            return true;
        }

        var payload = new { event_ids = eventIds };
        var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("sync/ack", content);
        return response.IsSuccessStatusCode;
    }

    public sealed class RemoteNoteEvent
    {
        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }

        [JsonPropertyName("note")]
        public RemoteNote? Note { get; set; }
    }

    public sealed class RemoteNote
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("updated_at")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("deleted")]
        public bool Deleted { get; set; }

        [JsonPropertyName("created_by_user_id")]
        public string? CreatedByUserId { get; set; }

        [JsonPropertyName("target_user_id")]
        public string? TargetUserId { get; set; }

        [JsonPropertyName("group_id")]
        public string? GroupId { get; set; }
    }
}
