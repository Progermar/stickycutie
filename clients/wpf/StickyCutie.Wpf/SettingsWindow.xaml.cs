using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using StickyCutie.Wpf.Alarms;
using StickyCutie.Wpf.Data;
using StickyCutie.Wpf.Services;

namespace StickyCutie.Wpf;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<GroupLocal> Groups { get; } = new();
    public ObservableCollection<UserLocal> Users { get; } = new();
    public ObservableCollection<NoteListItem> Notes { get; } = new();
    public ObservableCollection<GroupInviteItem> Invites { get; } = new();

    GroupLocal? _selectedGroup;
    UserLocal? _selectedUser;
    string? _activeGroupId;
    string? _activeUserId;
    bool _isCurrentAuthorAdmin;
    string? _alarmSoundPath;
    NoteListItem? _selectedNote;
    bool _syncingGroups;
    bool _syncingUsers;
    GroupInviteItem? _selectedInvite;

    public GroupLocal? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup != value)
            {
                _selectedGroup = value;
                OnPropertyChanged();
            }
        }
    }

    public UserLocal? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (_selectedUser != value)
            {
                _selectedUser = value;
                OnPropertyChanged();
            }
        }
    }

    public string ActiveGroupSummary => string.IsNullOrWhiteSpace(_activeGroupId)
        ? "Grupo ativo: nenhum"
        : $"Grupo ativo: {Groups.FirstOrDefault(g => g.Id == _activeGroupId)?.Name ?? _activeGroupId}";

    public string ActiveUserSummary => string.IsNullOrWhiteSpace(_activeUserId)
        ? "Destinatário padrão: nenhum"
        : $"Destinatário padrão: {Users.FirstOrDefault(u => u.Id == _activeUserId)?.Name ?? _activeUserId}{(Users.FirstOrDefault(u => u.Id == _activeUserId)?.IsAdmin == true ? " (admin)" : string.Empty)}";

    public string AlarmSoundSummary => string.IsNullOrWhiteSpace(_alarmSoundPath)
        ? "Nenhum som personalizado. O Windows tocará o alerta padrão."
        : _alarmSoundPath;

    public NoteListItem? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote != value)
            {
                _selectedNote = value;
                OnPropertyChanged();
            }
        }
    }

    public GroupInviteItem? SelectedInvite
    {
        get => _selectedInvite;
        set
        {
            if (_selectedInvite != value)
            {
                _selectedInvite = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) =>
        {
            await RefreshUsersAsync();
            await RefreshGroupsAsync();
            await RefreshAlarmSettingsAsync();
            await RefreshNotesAsync();
        };
    }

    async Task RefreshGroupsAsync()
    {
        await SyncGroupsFromBackendAsync();

        Groups.Clear();
        var groups = await App.Database.GetGroupsAsync();
        foreach (var group in groups.OrderBy(g => g.Name))
        {
            Groups.Add(group);
        }

        _activeGroupId = App.CurrentGroupId;
        SelectedGroup = Groups.FirstOrDefault(g => g.Id == _activeGroupId);
        OnPropertyChanged(nameof(ActiveGroupSummary));
        UpdateGroupUiState();
        await RefreshInvitesAsync();
    }

    async Task RefreshUsersAsync()
    {
        await SyncUsersFromBackendAsync();

        Users.Clear();
        var users = await App.Database.GetUsersAsync();
        foreach (var user in users.OrderBy(u => u.Name))
        {
            Users.Add(user);
        }

        _activeUserId = App.CurrentRecipientId;
        SelectedUser = Users.FirstOrDefault(u => u.Id == _activeUserId);
        OnPropertyChanged(nameof(ActiveUserSummary));
        _isCurrentAuthorAdmin = App.IsCurrentAuthorAdmin;
        UpdateGroupUiState();
        UpdateResetButtonState();
    }

    async Task RefreshAlarmSettingsAsync()
    {
        _alarmSoundPath = await App.Database.GetSettingAsync("alarm_sound_path");
        OnPropertyChanged(nameof(AlarmSoundSummary));
    }

    async Task SyncGroupsFromBackendAsync()
    {
        if (_syncingGroups)
        {
            return;
        }

        _syncingGroups = true;
        try
        {
            var remote = await ApiService.GetGroupsAsync();
            foreach (var group in remote)
            {
                var local = new GroupLocal
                {
                    Id = group.Id,
                    Name = group.Name,
                    Description = group.Description,
                    JoinedAt = new DateTimeOffset(group.CreatedAt).ToUnixTimeSeconds(),
                    UpdatedAt = group.UpdatedAt.HasValue ? new DateTimeOffset(group.UpdatedAt.Value).ToUnixTimeSeconds() : NoteDefaults.Now()
                };
                await App.Database.UpsertGroupAsync(local);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Falha ao sincronizar grupos: {ex.Message}");
        }
        finally
        {
            _syncingGroups = false;
        }
    }

    async Task SyncUsersFromBackendAsync()
    {
        if (_syncingUsers || string.IsNullOrWhiteSpace(App.CurrentGroupId))
        {
            return;
        }

        _syncingUsers = true;
        try
        {
            var remote = await ApiService.GetUsersByGroupAsync(App.CurrentGroupId);
            foreach (var user in remote)
            {
                var local = new UserLocal
                {
                    Id = user.Id,
                    GroupId = user.GroupId,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    CreatedAt = new DateTimeOffset(user.CreatedAt).ToUnixTimeSeconds(),
                    UpdatedAt = new DateTimeOffset(user.UpdatedAt).ToUnixTimeSeconds(),
                    IsAdmin = user.IsAdmin
                };
                await App.Database.UpsertUserAsync(local);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Falha ao sincronizar usuários: {ex.Message}");
        }
        finally
        {
            _syncingUsers = false;
        }
    }

    async Task RefreshNotesAsync()
    {
        Notes.Clear();
        try
        {
            var notes = await App.Database.GetNotesAsync(App.CurrentGroupId);
            var users = await App.Database.GetUsersAsync();
            var userMap = users.ToDictionary(u => u.Id, u => u.Name ?? "(sem nome)");
            var items = notes
                .Where(n => !n.Deleted)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NoteListItem
                {
                    Id = n.Id,
                    Author = ResolveName(userMap, n.CreatedByUserId),
                    Recipient = ResolveName(userMap, n.SourceUserId),
                    CreatedAt = n.CreatedAt,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? "Nota" : n.Title
                });

            foreach (var item in items)
            {
                Notes.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível carregar as notas: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string ResolveName(Dictionary<string, string> map, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Desconhecido";
        if (map.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return id;
    }

    void UpdateGroupUiState()
    {
        if (GroupButtonsPanel == null || GroupsTab == null || GroupInfoText == null)
        {
            return;
        }

        GroupsTab.IsEnabled = _isCurrentAuthorAdmin;
        GroupButtonsPanel.IsEnabled = _isCurrentAuthorAdmin;
        GroupInfoText.Text = _isCurrentAuthorAdmin
            ? "Selecione um grupo para gerenciar ou definir como ativo."
            : "Somente administradores podem gerenciar os grupos.";
        if (InvitePanel != null)
        {
            InvitePanel.Visibility = _isCurrentAuthorAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    void UpdateResetButtonState()
    {
        if (ResetButton != null)
        {
            ResetButton.Visibility = _isCurrentAuthorAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GroupEditorWindow();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            GroupLocal? group = null;
            try
            {
                var apiGroup = await ApiService.CreateGroupAsync(dialog.GroupName, dialog.GroupDescription);
                group = new GroupLocal
                {
                    Id = apiGroup.Id,
                    Name = apiGroup.Name,
                    Description = apiGroup.Description,
                    JoinedAt = new DateTimeOffset(apiGroup.CreatedAt).ToUnixTimeSeconds(),
                    UpdatedAt = apiGroup.UpdatedAt.HasValue ? new DateTimeOffset(apiGroup.UpdatedAt.Value).ToUnixTimeSeconds() : NoteDefaults.Now()
                };

                await App.Database.UpsertGroupAsync(group);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível criar o grupo: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await RefreshGroupsAsync();
            if (group != null && string.IsNullOrWhiteSpace(_activeGroupId))
            {
                await App.Current.ChangeGroupAsync(group.Id);
                await RefreshGroupsAsync();
            }
        }
    }

    async void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup == null)
        {
            MessageBox.Show("Selecione um grupo para editar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new GroupEditorWindow(SelectedGroup.Name, SelectedGroup.Description);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            var group = new GroupLocal
            {
                Id = SelectedGroup.Id,
                Name = dialog.GroupName,
                Description = dialog.GroupDescription,
                JoinedAt = SelectedGroup.JoinedAt,
                UpdatedAt = NoteDefaults.Now()
            };

            await App.Database.UpsertGroupAsync(group);
            await RefreshGroupsAsync();
        }
    }

    async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup == null)
        {
            MessageBox.Show("Selecione um grupo para excluir.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedGroup.Id == _activeGroupId)
        {
            MessageBox.Show("NÃƒÂ£o ÃƒÂ© possÃƒÂ­vel excluir o grupo ativo.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Excluir o grupo \"{SelectedGroup.Name}\"?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await App.Database.DeleteGroupAsync(SelectedGroup.Id);
            await RefreshGroupsAsync();
        }
    }

    async void SetActiveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup == null)
        {
            MessageBox.Show("Selecione um grupo para ativar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await App.Current.ChangeGroupAsync(SelectedGroup.Id);
        await RefreshGroupsAsync();
    }

    async void CreateUser_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UserEditorWindow(requirePassword: true, allowAdminToggle: false, title: "Novo usuário");
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            if (string.IsNullOrWhiteSpace(App.CurrentGroupId))
            {
                MessageBox.Show("Ative ou crie um grupo antes de adicionar usuários.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var now = NoteDefaults.Now();
            var user = new UserLocal
            {
                Name = dialog.UserName,
                Email = dialog.UserEmail,
                Phone = dialog.UserPhone,
                CreatedAt = now,
                UpdatedAt = now,
                IsAdmin = dialog.IsAdmin
            };

            try
            {
                var result = await ApiService.RegisterUserAsync(new ApiService.UserRegisterRequest
                {
                    GroupId = App.CurrentGroupId,
                    Name = dialog.UserName,
                    Email = dialog.UserEmail,
                    Phone = dialog.UserPhone,
                    Password = dialog.PlainPassword ?? string.Empty,
                    IsAdmin = dialog.IsAdmin
                });

                App.Current.SetAccessToken(result.AccessToken);
                user.Id = result.Id;
                user.GroupId = result.GroupId;

                await App.Database.UpsertUserAsync(user);
                await RefreshUsersAsync();
                if (string.IsNullOrWhiteSpace(_activeUserId))
                {
                    await App.Current.SetRecipientUserAsync(user.Id);
                    await RefreshUsersAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível registrar o usuário: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    async void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("Selecione um usuário para editar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new UserEditorWindow(requirePassword: false, allowAdminToggle: true, name: SelectedUser.Name, email: SelectedUser.Email, phone: SelectedUser.Phone, isAdmin: SelectedUser.IsAdmin, existingPasswordHash: SelectedUser.PasswordHash, title: "Editar usuário");
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            if (SelectedUser.IsAdmin && !dialog.IsAdmin && !HasAnotherAdmin(SelectedUser.Id))
            {
                MessageBox.Show("É necessário manter pelo menos um administrador ativo.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await ApiService.UpdateUserAsync(SelectedUser.Id, new ApiService.UserUpdateRequest
                {
                    Name = dialog.UserName,
                    Email = dialog.UserEmail,
                    Phone = dialog.UserPhone,
                    IsAdmin = dialog.IsAdmin,
                    Password = dialog.PlainPassword
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível atualizar o usuário: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var user = new UserLocal
            {
                Id = SelectedUser.Id,
                GroupId = SelectedUser.GroupId,
                Name = dialog.UserName,
                Email = dialog.UserEmail,
                Phone = dialog.UserPhone,
                CreatedAt = SelectedUser.CreatedAt,
                UpdatedAt = NoteDefaults.Now(),
                IsAdmin = dialog.IsAdmin
            };

            await App.Database.UpsertUserAsync(user);
            await RefreshUsersAsync();
        }
    }

    async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("Selecione um usuário para excluir.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedUser.Id == _activeUserId)
        {
            MessageBox.Show("Não é possível excluir o Destinatário padrão.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedUser.IsAdmin && !HasAnotherAdmin(SelectedUser.Id))
        {
            MessageBox.Show("Não é possível excluir o último administrador.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Excluir o usuário \"{SelectedUser.Name}\"?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await ApiService.DeleteUserAsync(SelectedUser.Id);
            await App.Database.DeleteUserAsync(SelectedUser.Id);
            await RefreshUsersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível excluir o usuário: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    async void SetActiveUser_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser == null)
        {
            MessageBox.Show("Selecione um usuÃƒÂ¡rio para ativar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var target = SelectedUser;
        await App.Current.SetRecipientUserAsync(target.Id);
        await RefreshUsersAsync();
        MessageBox.Show($"DestinatÃƒÂ¡rio padrÃƒÂ£o alterado para {target.Name}.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
    }


    async Task RefreshInvitesAsync()
    {
        Invites.Clear();
        if (!_isCurrentAuthorAdmin || string.IsNullOrWhiteSpace(App.CurrentGroupId))
        {
            return;
        }

        try
        {
            var invites = await ApiService.GetInvitesAsync(App.CurrentGroupId);
            foreach (var invite in invites.OrderByDescending(i => i.ExpiresAt))
            {
                Invites.Add(new GroupInviteItem(invite));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Falha ao carregar convites: {ex.Message}");
        }
    }

    async void JoinWithCode_Click(object sender, RoutedEventArgs e)
    {
        var currentAuthor = string.IsNullOrWhiteSpace(App.CurrentAuthorId)
            ? null
            : await App.Database.GetUserAsync(App.CurrentAuthorId);
        var join = new JoinGroupWindow(currentAuthor?.Name, currentAuthor?.Email)
        {
            Owner = this
        };

        if (join.ShowDialog() == true)
        {
            await RefreshGroupsAsync();
            await RefreshUsersAsync();
        }
    }

    void GenerateInvite_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrentAuthorAdmin || string.IsNullOrWhiteSpace(App.CurrentGroupId))
        {
            MessageBox.Show("Ative um grupo antes de gerar convites.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new InviteDialogWindow(App.CurrentGroupId)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            Invites.Insert(0, new GroupInviteItem(dialog.Result));
        }
    }

    void CopyInviteToken_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInvite == null)
        {
            MessageBox.Show("Selecione um convite para copiar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(SelectedInvite.Token);
        MessageBox.Show("Token copiado para a área de transferência.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    async void RevokeInvite_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInvite == null)
        {
            MessageBox.Show("Selecione um convite para revogar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show("Revogar este convite?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await ApiService.DeleteInviteAsync(SelectedInvite.Token);
            Invites.Remove(SelectedInvite);
            SelectedInvite = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível revogar o convite: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    async void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCurrentAuthorAdmin)
        {
            MessageBox.Show("Apenas administradores podem resetar o sistema.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show("Tem certeza de que deseja resetar o sistema? Esta ação apagará todos os dados.", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (MessageBox.Show("Confirme novamente: o reset é irreversível. Deseja prosseguir?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await App.Current.ResetEnvironmentAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    async void ChooseAlarmSound_Click(object sender, RoutedEventArgs e)
    {
        var folder = GetAlarmFolder();
        Directory.CreateDirectory(folder);
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar som do alarme",
            Filter = "Áudio|*.wav;*.mp3;*.wma;*.aac",
            InitialDirectory = folder,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            string destination;
            try
            {
                var fileName = Path.GetFileName(dialog.FileName);
                destination = Path.Combine(folder, fileName);
                if (!string.Equals(dialog.FileName, destination, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(dialog.FileName, destination, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível copiar o arquivo de áudio: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await App.Database.SetSettingAsync("alarm_sound_path", destination);
            AlarmManager.SetCustomSound(destination);
            _alarmSoundPath = destination;
            OnPropertyChanged(nameof(AlarmSoundSummary));
            MessageBox.Show("Som personalizado atualizado.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    async void RefreshNotesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshNotesAsync();
    }

    async void DeleteNoteFromList_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedNote == null)
        {
            MessageBox.Show("Selecione uma nota para excluir.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Excluir a nota criada em {SelectedNote.CreatedAtText} por {SelectedNote.Author}?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await App.Database.SoftDeleteNoteAsync(SelectedNote.Id, NoteDefaults.Now());
            var window = App.Current.GetNoteWindow(SelectedNote.Id);
            window?.Close();
            Notes.Remove(SelectedNote);
            SelectedNote = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível excluir a nota: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public class NoteListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = "Nota";
        public string Author { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
        public string CreatedAtText => DateTimeOffset.FromUnixTimeSeconds(CreatedAt).LocalDateTime.ToString("g");
    }

    public class GroupInviteItem
    {
        public GroupInviteItem()
        {
        }

        public GroupInviteItem(ApiService.GroupInviteDto dto)
        {
            Token = dto.Token;
            Email = dto.Email;
            Status = dto.Status;
            ExpiresAt = dto.ExpiresAt.ToLocalTime();
        }

        public string Token { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string ExpiresAtText => ExpiresAt.ToString("g");
    }

    void OpenAlarmFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = GetAlarmFolder();
        Directory.CreateDirectory(folder);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir a pasta: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string GetAlarmFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyCutie",
            "alarms");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool HasAnotherAdmin(string? excludeId = null)
        => Users.Any(u => u.IsAdmin && (excludeId == null || u.Id != excludeId));

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.InvalidateAdminSession();
    }
}
