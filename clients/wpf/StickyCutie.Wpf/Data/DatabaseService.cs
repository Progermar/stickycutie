using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace StickyCutie.Wpf.Data;

public sealed class DatabaseService
{
    readonly string _databasePath;
    readonly string _connectionString;

    public DatabaseService(string? databasePath = null)
    {
        _databasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyCutie",
            "stickycutie.db");

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        _connectionString = $"Data Source={_databasePath}";
    }

    public async Task InitializeAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var createStatements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                name TEXT,
                email TEXT,
                phone TEXT,
                password_hash TEXT,
                created_at INTEGER,
                updated_at INTEGER,
                is_admin INTEGER
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS groups_local (
                id TEXT PRIMARY KEY,
                name TEXT,
                description TEXT,
                joined_at INTEGER,
                updated_at INTEGER
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS notes_local (
                id TEXT PRIMARY KEY,
                local_id TEXT,
                server_id TEXT,
                title TEXT,
                content TEXT,
                color TEXT,
                theme TEXT,
                x INTEGER,
                y INTEGER,
                width INTEGER,
                height INTEGER,
                locked INTEGER,
                lock_password TEXT,
                alarm_enabled INTEGER,
                alarm_time INTEGER,
                alarm_repeat TEXT,
                photo_mode INTEGER,
                updated_at INTEGER,
                created_at INTEGER,
                deleted INTEGER,
                target_user_id TEXT,
                source_user_id TEXT,
                created_by_user_id TEXT,
                group_id TEXT
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS note_images (
                id TEXT PRIMARY KEY,
                note_id TEXT NOT NULL,
                path TEXT,
                order_index INTEGER,
                duration INTEGER,
                created_at INTEGER
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS alarms_local (
                id TEXT PRIMARY KEY,
                note_id TEXT NOT NULL,
                alarm_at INTEGER,
                snooze_until INTEGER,
                is_enabled INTEGER,
                created_at INTEGER,
                updated_at INTEGER
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS settings_local (
                key TEXT PRIMARY KEY,
                value TEXT
            );
            """
        };

        foreach (var statement in createStatements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }
        await EnsureColumnsAsync(connection);
    }

    static async Task EnsureColumnsAsync(SqliteConnection connection)
    {
        await TryAddColumnAsync(connection, "users", "is_admin INTEGER DEFAULT 0");
        await TryAddColumnAsync(connection, "notes_local", "created_by_user_id TEXT");
    }

    static async Task TryAddColumnAsync(SqliteConnection connection, string table, string definition)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition};";
            await command.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            // column already exists
        }
    }

    #region Users

    public Task UpsertUserAsync(UserLocal user)
    {
        const string sql =
            """
            INSERT INTO users (id, name, email, phone, password_hash, created_at, updated_at, is_admin)
            VALUES (@id, @name, @email, @phone, @password_hash, @created_at, @updated_at, @is_admin)
            ON CONFLICT(id) DO UPDATE SET
                name=@name,
                email=@email,
                phone=@phone,
                password_hash=@password_hash,
                created_at=@created_at,
                updated_at=@updated_at,
                is_admin=@is_admin;
            """;

        return ExecuteAsync(sql,
            ("@id", user.Id),
            ("@name", user.Name),
            ("@email", user.Email),
            ("@phone", user.Phone),
            ("@password_hash", user.PasswordHash),
            ("@created_at", user.CreatedAt),
            ("@updated_at", user.UpdatedAt),
            ("@is_admin", BoolToInt(user.IsAdmin)));
    }

    public async Task<UserLocal?> GetUserAsync(string id)
    {
        const string sql = "SELECT * FROM users WHERE id=@id LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<UserLocal>> GetUsersAsync()
    {
        const string sql = "SELECT * FROM users;";
        var list = new List<UserLocal>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapUser(reader));
        }

        return list;
    }

    public Task DeleteUserAsync(string id)
    {
        const string sql = "DELETE FROM users WHERE id=@id;";
        return ExecuteAsync(sql, ("@id", id));
    }

    static UserLocal MapUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.IsDBNull(1) ? null : reader.GetString(1),
        Email = reader.IsDBNull(2) ? null : reader.GetString(2),
        Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
        PasswordHash = reader.IsDBNull(4) ? null : reader.GetString(4),
        CreatedAt = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
        UpdatedAt = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
        IsAdmin = reader.IsDBNull(7) ? false : IntToBool(reader.GetInt32(7))
    };

    #endregion

    #region Groups

    public Task UpsertGroupAsync(GroupLocal group)
    {
        const string sql =
            """
            INSERT INTO groups_local (id, name, description, joined_at, updated_at)
            VALUES (@id, @name, @description, @joined_at, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                name=@name,
                description=@description,
                joined_at=@joined_at,
                updated_at=@updated_at;
            """;

        return ExecuteAsync(sql,
            ("@id", group.Id),
            ("@name", group.Name),
            ("@description", group.Description),
            ("@joined_at", group.JoinedAt),
            ("@updated_at", group.UpdatedAt));
    }

    public async Task<GroupLocal?> GetGroupAsync(string id)
    {
        const string sql = "SELECT * FROM groups_local WHERE id=@id LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new GroupLocal
            {
                Id = reader.GetString(0),
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                JoinedAt = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                UpdatedAt = reader.IsDBNull(4) ? 0 : reader.GetInt64(4)
            };
        }

        return null;
    }

    public Task DeleteGroupAsync(string id)
    {
        const string sql = "DELETE FROM groups_local WHERE id=@id;";
        return ExecuteAsync(sql, ("@id", id));
    }

    public async Task<IReadOnlyList<GroupLocal>> GetGroupsAsync()
    {
        const string sql = "SELECT * FROM groups_local;";
        var list = new List<GroupLocal>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new GroupLocal
            {
                Id = reader.GetString(0),
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                JoinedAt = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                UpdatedAt = reader.IsDBNull(4) ? 0 : reader.GetInt64(4)
            });
        }

        return list;
    }

    #endregion

    #region Notes

    public Task UpsertNoteAsync(NoteLocal note)
    {
        const string sql =
            """
            INSERT INTO notes_local (id, local_id, server_id, title, content, color, theme, x, y, width, height, locked, lock_password, alarm_enabled, alarm_time, alarm_repeat, photo_mode, updated_at, created_at, deleted, target_user_id, source_user_id, created_by_user_id, group_id)
            VALUES (@id, @local_id, @server_id, @title, @content, @color, @theme, @x, @y, @width, @height, @locked, @lock_password, @alarm_enabled, @alarm_time, @alarm_repeat, @photo_mode, @updated_at, @created_at, @deleted, @target_user_id, @source_user_id, @created_by_user_id, @group_id)
            ON CONFLICT(id) DO UPDATE SET
                local_id=@local_id,
                server_id=@server_id,
                title=@title,
                content=@content,
                color=@color,
                theme=@theme,
                x=@x,
                y=@y,
                width=@width,
                height=@height,
                locked=@locked,
                lock_password=@lock_password,
                alarm_enabled=@alarm_enabled,
                alarm_time=@alarm_time,
                alarm_repeat=@alarm_repeat,
                photo_mode=@photo_mode,
                updated_at=@updated_at,
                created_at=@created_at,
                deleted=@deleted,
                target_user_id=@target_user_id,
                source_user_id=@source_user_id,
                created_by_user_id=@created_by_user_id,
                group_id=@group_id;
            """;

        return ExecuteAsync(sql,
            ("@id", note.Id),
            ("@local_id", note.LocalId),
            ("@server_id", note.ServerId),
            ("@title", note.Title),
            ("@content", note.Content),
            ("@color", note.Color),
            ("@theme", note.Theme),
            ("@x", note.X),
            ("@y", note.Y),
            ("@width", note.Width),
            ("@height", note.Height),
            ("@locked", BoolToInt(note.Locked)),
            ("@lock_password", note.LockPassword),
            ("@alarm_enabled", BoolToInt(note.AlarmEnabled)),
            ("@alarm_time", note.AlarmTime),
            ("@alarm_repeat", note.AlarmRepeat),
            ("@photo_mode", BoolToInt(note.PhotoMode)),
            ("@updated_at", note.UpdatedAt),
            ("@created_at", note.CreatedAt),
            ("@deleted", BoolToInt(note.Deleted)),
            ("@target_user_id", note.TargetUserId),
            ("@source_user_id", note.SourceUserId),
            ("@created_by_user_id", note.CreatedByUserId),
            ("@group_id", note.GroupId));
    }

    public async Task<IReadOnlyList<NoteLocal>> GetNotesAsync(string? groupId = null)
    {
        var sql = groupId == null
            ? "SELECT * FROM notes_local WHERE deleted=0;"
            : "SELECT * FROM notes_local WHERE deleted=0 AND group_id=@group_id;";
        var list = new List<NoteLocal>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (groupId != null)
        {
            command.Parameters.AddWithValue("@group_id", groupId);
        }
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapNote(reader));
        }

        return list;
    }

    public Task SoftDeleteNoteAsync(string id, long updatedAt)
    {
        const string sql = "UPDATE notes_local SET deleted=1, updated_at=@updated_at WHERE id=@id;";
        return ExecuteAsync(sql, ("@id", id), ("@updated_at", updatedAt));
    }

    static NoteLocal MapNote(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        LocalId = reader.IsDBNull(1) ? null : reader.GetString(1),
        ServerId = reader.IsDBNull(2) ? null : reader.GetString(2),
        Title = reader.IsDBNull(3) ? null : reader.GetString(3),
        Content = reader.IsDBNull(4) ? null : reader.GetString(4),
        Color = reader.IsDBNull(5) ? null : reader.GetString(5),
        Theme = reader.IsDBNull(6) ? null : reader.GetString(6),
        X = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
        Y = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
        Width = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
        Height = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
        Locked = reader.IsDBNull(11) ? false : IntToBool(reader.GetInt32(11)),
        LockPassword = reader.IsDBNull(12) ? null : reader.GetString(12),
        AlarmEnabled = reader.IsDBNull(13) ? false : IntToBool(reader.GetInt32(13)),
        AlarmTime = reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
        AlarmRepeat = reader.IsDBNull(15) ? null : reader.GetString(15),
        PhotoMode = reader.IsDBNull(16) ? false : IntToBool(reader.GetInt32(16)),
        UpdatedAt = reader.IsDBNull(17) ? 0 : reader.GetInt64(17),
        CreatedAt = reader.IsDBNull(18) ? 0 : reader.GetInt64(18),
        Deleted = reader.IsDBNull(19) ? false : IntToBool(reader.GetInt32(19)),
        TargetUserId = reader.IsDBNull(20) ? null : reader.GetString(20),
        SourceUserId = reader.IsDBNull(21) ? null : reader.GetString(21),
        CreatedByUserId = reader.IsDBNull(22) ? null : reader.GetString(22),
        GroupId = reader.IsDBNull(23) ? null : reader.GetString(23)
    };

    static AlarmLocal MapAlarm(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        NoteId = reader.GetString(1),
        AlarmAt = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
        SnoozeUntil = reader.IsDBNull(3) ? null : reader.GetInt64(3),
        IsEnabled = reader.IsDBNull(4) ? false : IntToBool(reader.GetInt32(4)),
        CreatedAt = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
        UpdatedAt = reader.IsDBNull(6) ? 0 : reader.GetInt64(6)
    };

    public async Task<NoteLocal?> GetNoteAsync(string id)
    {
        const string sql = "SELECT * FROM notes_local WHERE id=@id LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapNote(reader);
        }

        return null;
    }

    #endregion

    #region Note Images

    public Task UpsertNoteImageAsync(NoteImage image)
    {
        const string sql =
            """
            INSERT INTO note_images (id, note_id, path, order_index, duration, created_at)
            VALUES (@id, @note_id, @path, @order_index, @duration, @created_at)
            ON CONFLICT(id) DO UPDATE SET
                note_id=@note_id,
                path=@path,
                order_index=@order_index,
                duration=@duration,
                created_at=@created_at;
            """;

        return ExecuteAsync(sql,
            ("@id", image.Id),
            ("@note_id", image.NoteId),
            ("@path", image.Path),
            ("@order_index", image.OrderIndex),
            ("@duration", image.Duration),
            ("@created_at", image.CreatedAt));
    }

    public async Task<IReadOnlyList<NoteImage>> GetImagesAsync(string noteId)
    {
        const string sql = "SELECT * FROM note_images WHERE note_id=@note_id ORDER BY order_index;";
        var list = new List<NoteImage>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@note_id", noteId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new NoteImage
            {
                Id = reader.GetString(0),
                NoteId = reader.GetString(1),
                Path = reader.IsDBNull(2) ? null : reader.GetString(2),
                OrderIndex = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Duration = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                CreatedAt = reader.IsDBNull(5) ? 0 : reader.GetInt64(5)
            });
        }

        return list;
    }

    public Task DeleteImageAsync(string id)
    {
        const string sql = "DELETE FROM note_images WHERE id=@id;";
        return ExecuteAsync(sql, ("@id", id));
    }

    public Task DeleteImagesByNoteAsync(string noteId)
    {
        const string sql = "DELETE FROM note_images WHERE note_id=@note_id;";
        return ExecuteAsync(sql, ("@note_id", noteId));
    }

    #endregion

    #region Alarms

    public Task UpsertAlarmAsync(AlarmLocal alarm)
    {
        const string sql =
            """
            INSERT INTO alarms_local (id, note_id, alarm_at, snooze_until, is_enabled, created_at, updated_at)
            VALUES (@id, @note_id, @alarm_at, @snooze_until, @is_enabled, @created_at, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                note_id=@note_id,
                alarm_at=@alarm_at,
                snooze_until=@snooze_until,
                is_enabled=@is_enabled,
                created_at=@created_at,
                updated_at=@updated_at;
            """;

        return ExecuteAsync(sql,
            ("@id", alarm.Id),
            ("@note_id", alarm.NoteId),
            ("@alarm_at", alarm.AlarmAt),
            ("@snooze_until", alarm.SnoozeUntil),
            ("@is_enabled", BoolToInt(alarm.IsEnabled)),
            ("@created_at", alarm.CreatedAt),
            ("@updated_at", alarm.UpdatedAt));
    }

    public async Task<AlarmLocal?> GetAlarmAsync(string noteId)
    {
        const string sql = "SELECT * FROM alarms_local WHERE note_id=@note_id LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@note_id", noteId);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapAlarm(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<AlarmLocal>> GetDueAlarmsAsync(long now)
    {
        const string sql =
            """
            SELECT * FROM alarms_local
            WHERE is_enabled=1
              AND alarm_at IS NOT NULL
              AND (
                    (snooze_until IS NULL AND alarm_at<=@now)
                    OR (snooze_until IS NOT NULL AND snooze_until<=@now)
                  );
            """;
        var list = new List<AlarmLocal>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@now", now);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapAlarm(reader));
        }

        return list;
    }

    public Task DeleteAlarmAsync(string id)
    {
        const string sql = "DELETE FROM alarms_local WHERE id=@id;";
        return ExecuteAsync(sql, ("@id", id));
    }

    public Task DeleteAlarmsByNoteAsync(string noteId)
    {
        const string sql = "DELETE FROM alarms_local WHERE note_id=@note_id;";
        return ExecuteAsync(sql, ("@note_id", noteId));
    }

    #endregion

    #region Settings

    public Task SetSettingAsync(string key, string? value)
    {
        const string sql =
            """
            INSERT INTO settings_local (key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value=@value;
            """;

        return ExecuteAsync(sql, ("@key", key), ("@value", value));
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        const string sql = "SELECT value FROM settings_local WHERE key=@key LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@key", key);
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync()
    {
        const string sql = "SELECT key, value FROM settings_local;";
        var dictionary = new Dictionary<string, string?>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dictionary[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        return dictionary;
    }

    #endregion

    SqliteConnection CreateConnection() => new(_connectionString);

    Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
        => ExecuteAsyncInternal(sql, parameters);

    async Task ExecuteAsyncInternal(string sql, (string Name, object? Value)[] parameters)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    static int BoolToInt(bool value) => value ? 1 : 0;
    static bool IntToBool(int value) => value != 0;
}
