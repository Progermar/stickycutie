using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StickyCutie.Wpf.Data;
using StickyCutie.Wpf.Alarms;
using StickyCutie.Wpf.Services;

namespace StickyCutie.Wpf;

public partial class App : Application
{
    public static DatabaseService Database { get; } = new();
    public static new App Current => (App)Application.Current;
    public NoteLocal? LastCreatedNote { get; private set; }
public static string CurrentRecipientId { get; private set; } = string.Empty;
public static string CurrentGroupId { get; private set; } = string.Empty;
public static string CurrentAuthorId { get; private set; } = string.Empty;
public static bool IsCurrentAuthorAdmin { get; private set; }
public static bool IsAdminSessionAuthenticated { get; private set; }
public static string? CurrentAuthorPasswordHash { get; private set; }

    readonly HashSet<MainWindow> _noteWindows = new();
    MainControlWindow? _controlWindow;
    bool _isRestoringNotes;
    SyncService? _syncService;
    bool _ranSetupThisSession;

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Console.WriteLine($"[Config] API URL: {ConfigService.GetApiUrl()}");
        await Database.InitializeAsync();
        var contextReady = await EnsureContextAsync();
        if (!contextReady)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        _controlWindow = new MainControlWindow();
        MainWindow = _controlWindow;
        _controlWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        AlarmManager.Initialize(Database);
        _syncService = new SyncService(Database);
        await _syncService.StartAsync();

        if (!string.IsNullOrWhiteSpace(CurrentGroupId))
        {
            await _controlWindow.Dispatcher.InvokeAsync(async () => await RestoreNotesAsync());
            await EnsureDefaultNoteAsync();
        }

        if (_ranSetupThisSession)
        {
            _ranSetupThisSession = false;
            var message = string.IsNullOrWhiteSpace(CurrentGroupId)
                ? "ConfiguraÃƒÂ§ÃƒÂ£o inicial concluÃƒÂ­da! Use o botÃƒÂ£o Ã¢Å¡â„¢ para cadastrar e ativar o primeiro grupo antes de criar notas."
                : "ConfiguraÃƒÂ§ÃƒÂ£o inicial concluÃƒÂ­da! Use o botÃƒÂ£o Ã¢Å¡â„¢ para gerenciar grupos e usuÃƒÂ¡rios.";
            MessageBox.Show(message, "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            _controlWindow.Activate();
        }
        else if (string.IsNullOrWhiteSpace(CurrentGroupId))
        {
            MessageBox.Show("Nenhum grupo ativo. Abra as ConfiguraÃƒÂ§ÃƒÂµes (Ã¢Å¡â„¢) para criar e ativar um grupo antes de usar as notas.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    void App_DispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString());
        e.Handled = true;
    }

    async Task<bool> EnsureContextAsync()
    {
        while (true)
        {
            var authorId = await Database.GetSettingAsync("current_author_id");
            UserLocal? author = null;
            if (!string.IsNullOrWhiteSpace(authorId))
            {
                author = await Database.GetUserAsync(authorId);
            }
            else
            {
                var fallbackId = await Database.GetSettingAsync("current_user_id");
                if (!string.IsNullOrWhiteSpace(fallbackId))
                {
                    var fallbackUser = await Database.GetUserAsync(fallbackId);
                    if (fallbackUser?.IsAdmin == true)
                    {
                        await Database.SetSettingAsync("current_author_id", fallbackUser.Id);
                        author = fallbackUser;
                    }
                }
            }

            if (author != null)
            {
                ApplyAuthor(author);
                await LoadCurrentGroupAsync();
                await EnsureRecipientInitializedAsync(author.Id);
                return true;
            }

            var setup = new SetupWindow(Database);
            if (MainWindow != null && !ReferenceEquals(MainWindow, setup))
            {
                setup.Owner = MainWindow;
            }

            var result = setup.ShowDialog();
            if (ReferenceEquals(MainWindow, setup))
            {
                MainWindow = null;
            }

            if (result != true)
            {
                return false;
            }

            _ranSetupThisSession = true;
        }
    }

    async Task LoadCurrentGroupAsync()
    {
        var groupId = await Database.GetSettingAsync("current_group_id");
        var group = string.IsNullOrWhiteSpace(groupId) ? null : await Database.GetGroupAsync(groupId);
        ApplyCurrentGroup(group);
    }

    async Task EnsureRecipientInitializedAsync(string fallbackUserId)
    {
        var recipientId = await Database.GetSettingAsync("current_user_id");
        var recipient = string.IsNullOrWhiteSpace(recipientId) ? null : await Database.GetUserAsync(recipientId);
        if (recipient == null)
        {
            recipientId = fallbackUserId;
            await Database.SetSettingAsync("current_user_id", recipientId);
        }

        CurrentRecipientId = recipientId ?? string.Empty;
    }

    async Task RestoreNotesAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentGroupId) || _isRestoringNotes)
        {
            return;
        }

        _isRestoringNotes = true;
        try
        {
            var notes = await Database.GetNotesAsync(CurrentGroupId);
            if (notes.Count == 0)
            {
                var bootstrap = NoteDefaults.Create(CurrentGroupId, CurrentRecipientId, CurrentAuthorId);
                await Database.UpsertNoteAsync(bootstrap);
                notes = new List<NoteLocal> { bootstrap };
            }

            foreach (var note in notes)
            {
                OpenNoteWindow(note);
            }
        }
        finally
        {
            _isRestoringNotes = false;
        }
    }

    public Task<MainWindow> CreateAndShowNoteAsync()
        => CreateNoteForRecipientAsync(CurrentRecipientId, null, null, null);

    public Task<MainWindow> CreatePersonalNoteAsync(string? title = null)
        => CreateNoteForRecipientAsync(CurrentAuthorId, null, null, title);

    public Task<MainWindow> CreateNoteForRecipientAsync(string? recipientId, string? initialText, DateTimeOffset? alarmDateTime, string? title = null)
        => CreateAndOpenNoteInternalAsync(recipientId, initialText, alarmDateTime, title);


    async Task<MainWindow> CreateAndOpenNoteInternalAsync(string? recipientId, string? initialText, DateTimeOffset? alarmDateTime, string? title)
    {
        if (string.IsNullOrWhiteSpace(CurrentGroupId) || string.IsNullOrWhiteSpace(CurrentAuthorId) || string.IsNullOrWhiteSpace(recipientId))
        {
            throw new InvalidOperationException("O contexto local não está pronto. Ative um grupo e selecione um usuário.");
        }

        var note = NoteDefaults.Create(CurrentGroupId, recipientId, CurrentAuthorId);
        if (!string.IsNullOrWhiteSpace(title))
        {
            note.Title = title.Trim();
        }
        if (!string.IsNullOrWhiteSpace(initialText))
        {
            note.Content = NoteDocumentBuilder.FromPlainText(initialText);
        }

        await Database.UpsertNoteAsync(note);
        LastCreatedNote = note;

        if (!string.IsNullOrWhiteSpace(recipientId) && !string.Equals(recipientId, CurrentAuthorId, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await ApiService.SendNoteAsync(note);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] Falha ao enviar nota: {ex.Message}");
                MessageBox.Show("Nota criada localmente, mas não foi possível sincronizar com o servidor.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        if (alarmDateTime.HasValue)
        {
            var unix = alarmDateTime.Value.ToUnixTimeSeconds();
            var now = NoteDefaults.Now();
            var alarm = new AlarmLocal
            {
                Id = Guid.NewGuid().ToString("N"),
                NoteId = note.Id,
                AlarmAt = unix,
                SnoozeUntil = null,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            note.AlarmEnabled = true;
            note.AlarmTime = unix;
            await Database.UpsertAlarmAsync(alarm);
            AlarmManager.NotifyChange(note.Id);
        }

        return OpenNoteWindow(note);
    }

    public MainWindow OpenNoteWindow(NoteLocal note)
    {
        var window = new MainWindow(note, Database);
        window.Closed += NoteWindow_Closed;
        _noteWindows.Add(window);
        window.Show();
        window.Activate();
        return window;
    }

    public MainWindow? GetNoteWindow(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return null;
        return _noteWindows.FirstOrDefault(n => string.Equals(n.NoteId, noteId, StringComparison.OrdinalIgnoreCase));
    }

    void NoteWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is MainWindow note)
        {
            note.Closed -= NoteWindow_Closed;
            _noteWindows.Remove(note);
        }
    }

    void CloseAllNotes()
    {
        foreach (var note in _noteWindows.ToArray())
        {
            note.Closed -= NoteWindow_Closed;
            note.Close();
        }

        _noteWindows.Clear();
    }

    public async Task ReloadNotesAsync()
    {
        CloseAllNotes();
        await RestoreNotesAsync();
    }

    public async Task ChangeGroupAsync(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId) || groupId == CurrentGroupId)
        {
            return;
        }

        var group = await Database.GetGroupAsync(groupId);
        if (group == null)
        {
            return;
        }

        await Database.SetSettingAsync("current_group_id", group.Id);
        ApplyCurrentGroup(group);
        await ReloadNotesAsync();
        await EnsureDefaultNoteAsync();
    }

    public async Task SetRecipientUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId == CurrentRecipientId)
        {
            return;
        }

        var user = await Database.GetUserAsync(userId);
        if (user == null)
        {
            return;
        }

        CurrentRecipientId = user.Id;
        await Database.SetSettingAsync("current_user_id", user.Id);
    }

    public static void AuthenticateAdminSession()
    {
        if (Current is App app)
        {
            app.SetAdminSession(true);
        }
    }

    public static void InvalidateAdminSession()
    {
        if (Current is App app)
        {
            app.SetAdminSession(false);
        }
    }

    void SetAdminSession(bool value) => IsAdminSessionAuthenticated = value;

    void ApplyAuthor(UserLocal user)
    {
        CurrentAuthorId = user.Id;
        CurrentAuthorPasswordHash = user.PasswordHash;
        IsCurrentAuthorAdmin = user.IsAdmin;
        SetAdminSession(false);
    }

    void ApplyCurrentGroup(GroupLocal? group)
    {
        CurrentGroupId = group?.Id ?? string.Empty;
        SetAdminSession(false);
    }

    async Task EnsureDefaultNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentGroupId) || string.IsNullOrWhiteSpace(CurrentRecipientId) || string.IsNullOrWhiteSpace(CurrentAuthorId)) return;
        var notes = await Database.GetNotesAsync(CurrentGroupId);
        if (notes.Count == 0)
        {
            await CreateAndShowNoteAsync();
        }
    }
}






