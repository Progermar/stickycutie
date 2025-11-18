namespace StickyCutie.Wpf.Data;

public class UserLocal
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? GroupId { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public bool IsAdmin { get; set; }
}

public class GroupLocal
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public long JoinedAt { get; set; }
    public long UpdatedAt { get; set; }
}

public class NoteLocal
{
    public string Id { get; set; } = string.Empty;
    public string? LocalId { get; set; }
    public string? ServerId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Color { get; set; }
    public string? Theme { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Locked { get; set; }
    public string? LockPassword { get; set; }
    public bool AlarmEnabled { get; set; }
    public long AlarmTime { get; set; }
    public string? AlarmRepeat { get; set; }
    public bool PhotoMode { get; set; }
    public long UpdatedAt { get; set; }
    public long CreatedAt { get; set; }
    public bool Deleted { get; set; }
    public string? TargetUserId { get; set; }
    public string? SourceUserId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? GroupId { get; set; }
}

public class NoteImage
{
    public string Id { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public string? Path { get; set; }
    public int OrderIndex { get; set; }
    public int Duration { get; set; }
    public long CreatedAt { get; set; }
}

public class AlarmLocal
{
    public string Id { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public long AlarmAt { get; set; }
    public long? SnoozeUntil { get; set; }
    public bool IsEnabled { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}

public class SettingLocal
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
