using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Services;

public static class ApiService
{
    static readonly string _baseUrl = ConfigService.GetApiUrl();
    static readonly HttpClient _httpClient;
    static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    static string? _accessToken;

    static ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    public static async Task ResetRemoteAsync()
    {
        using var response = await _httpClient.PostAsync("admin/reset", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
    }

    public static void SetAccessToken(string? token)
    {
        _accessToken = token;
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
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

    public static async Task<GroupDto> CreateGroupAsync(string name, string? description)
    {
        var payload = new { name, description };
        using var response = await _httpClient.PostAsJsonAsync("groups/create", payload);
        response.EnsureSuccessStatusCode();
        var api = await response.Content.ReadFromJsonAsync<GroupApi>(_jsonOptions)
                  ?? throw new InvalidOperationException("Resposta inválida ao criar grupo.");
        return api.ToDto();
    }

    public static async Task<List<GroupDto>> GetGroupsAsync()
    {
        using var response = await _httpClient.GetAsync("groups/list");
        response.EnsureSuccessStatusCode();
        var api = await response.Content.ReadFromJsonAsync<List<GroupApi>>(_jsonOptions);
        return api?.Select(a => a.ToDto()).ToList() ?? new List<GroupDto>();
    }

    public static async Task<UserRegisterResult> RegisterUserAsync(UserRegisterRequest request)
    {
        var payload = new
        {
            group_id = ParseServerId(request.GroupId),
            name = request.Name,
            email = request.Email,
            phone = request.Phone,
            password = request.Password,
            is_admin = request.IsAdmin
        };

        using var response = await _httpClient.PostAsJsonAsync("users/register", payload);
        response.EnsureSuccessStatusCode();
        var api = await response.Content.ReadFromJsonAsync<UserRegisterApi>(_jsonOptions)
                  ?? throw new InvalidOperationException("Resposta inválida ao registrar usuário.");
        return api.ToDto();
    }

    public static async Task<List<UserDto>> GetUsersByGroupAsync(string groupId)
    {
        using var response = await _httpClient.GetAsync($"users/by-group/{ParseServerId(groupId)}");
        response.EnsureSuccessStatusCode();
        var api = await response.Content.ReadFromJsonAsync<List<UserApi>>(_jsonOptions);
        return api?.Select(a => a.ToDto()).ToList() ?? new List<UserDto>();
    }

    public static async Task<UserDto> UpdateUserAsync(string userId, UserUpdateRequest request)
    {
        var payload = new
        {
            name = request.Name,
            email = request.Email,
            phone = request.Phone,
            is_admin = request.IsAdmin,
            password = request.Password
        };

        using var response = await _httpClient.PutAsJsonAsync($"users/{ParseServerId(userId)}", payload);
        response.EnsureSuccessStatusCode();
        var api = await response.Content.ReadFromJsonAsync<UserApi>(_jsonOptions)
                  ?? throw new InvalidOperationException("Resposta inválida ao atualizar usuário.");
        return api.ToDto();
    }

    public static async Task DeleteUserAsync(string userId)
    {
        using var response = await _httpClient.DeleteAsync($"users/{ParseServerId(userId)}");
        response.EnsureSuccessStatusCode();
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

    public static async Task<GroupInviteDto> CreateInviteAsync(string groupId, string? email)
    {
        var payload = new
        {
            email = string.IsNullOrWhiteSpace(email) ? null : email
        };
        using var response = await _httpClient.PostAsJsonAsync($"groups/{ParseServerId(groupId)}/invite", payload);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<GroupInviteDto>(_jsonOptions)
                  ?? throw new InvalidOperationException("Resposta inválida ao gerar convite.");
        dto.GroupId = groupId;
        return dto;
    }

    public static async Task<List<GroupInviteDto>> GetInvitesAsync(string groupId)
    {
        using var response = await _httpClient.GetAsync($"groups/{ParseServerId(groupId)}/invitations");
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<GroupInviteDto>>(_jsonOptions);
        if (list != null)
        {
            foreach (var item in list)
            {
                item.GroupId = groupId;
            }
        }
        return list ?? new List<GroupInviteDto>();
    }

    public static async Task DeleteInviteAsync(string token)
    {
        using var response = await _httpClient.DeleteAsync($"groups/invitations/{token}");
        response.EnsureSuccessStatusCode();
    }

    public static async Task<InvitePreviewDto> GetInvitePreviewAsync(string token)
    {
        using var response = await _httpClient.GetAsync($"groups/invitations/{token}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvitePreviewDto>(_jsonOptions)
               ?? throw new InvalidOperationException("Convite não encontrado.");
    }

    public static async Task<InviteAcceptResult> AcceptInviteAsync(string token, string name, string email, string? phone)
    {
        var payload = new
        {
            name,
            email,
            phone
        };
        using var response = await _httpClient.PostAsJsonAsync($"groups/invitations/{token}/accept", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InviteAcceptResult>(_jsonOptions)
               ?? throw new InvalidOperationException("Resposta inválida ao aceitar convite.");
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

    public sealed class GroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    class GroupApi
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }

        public GroupDto ToDto() => new()
        {
            Id = Id.ToString(),
            Name = Name,
            Description = Description,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public sealed class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    class UserApi
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("group_id")] public int GroupId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("is_admin")] public bool IsAdmin { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }

        public UserDto ToDto() => new()
        {
            Id = Id.ToString(),
            GroupId = GroupId.ToString(),
            Name = Name,
            Email = Email,
            Phone = Phone,
            IsAdmin = IsAdmin,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    class UserRegisterApi
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("group_id")] public int GroupId { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("token_type")] public string TokenType { get; set; } = "bearer";

        public UserRegisterResult ToDto() => new()
        {
            Id = Id.ToString(),
            GroupId = GroupId.ToString(),
            Email = Email,
            AccessToken = AccessToken,
            TokenType = TokenType
        };
    }

    public sealed class UserRegisterResult
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "bearer";
    }

    public sealed class UserRegisterRequest
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    public sealed class UserUpdateRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool? IsAdmin { get; set; }
        public string? Password { get; set; }
    }

    static int ParseServerId(string id)
    {
        if (int.TryParse(id, out var value))
        {
            return value;
        }
        throw new InvalidOperationException($"Id inválido: {id}");
    }

    static int? TryParseServerId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return int.TryParse(id, out var value) ? value : null;
    }

    public sealed class GroupInviteDto
    {
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; set; }
        [JsonPropertyName("created_by_user_id")] public int? CreatedByUserId { get; set; }
        public string GroupId { get; set; } = string.Empty;
    }

    public sealed class InvitePreviewDto
    {
        [JsonPropertyName("group_id")] public int GroupId { get; set; }
        [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; set; }
    }

    public sealed class InviteAcceptResult
    {
        [JsonPropertyName("group")] public GroupDto Group { get; set; } = new();
        [JsonPropertyName("user")] public UserDto User { get; set; } = new();
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    }
}
