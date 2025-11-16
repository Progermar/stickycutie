using System;
using System.Windows;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf;

public partial class SetupWindow : Window
{
    readonly DatabaseService _database;

    public string? CompletedUserId { get; private set; }

    public SetupWindow(DatabaseService database)
    {
        _database = database;
        InitializeComponent();
        Loaded += (_, _) => UserNameTextBox.Focus();
    }

    async void UserNextButton_Click(object sender, RoutedEventArgs e)
    {
        var name = UserNameTextBox.Text.Trim();
        var email = UserEmailTextBox.Text.Trim();
        var phone = UserPhoneTextBox.Text.Trim();
        var password = PasswordBox1.Password;
        var confirm = PasswordBox2.Password;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            MessageBox.Show("Informe nome e e-mail para continuar.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            MessageBox.Show("Informe uma senha (mÃ­nimo 4 caracteres).", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            PasswordBox1.Focus();
            return;
        }

        if (password != confirm)
        {
            MessageBox.Show("A confirmaÃ§Ã£o de senha nÃ£o confere.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox1.Clear();
            PasswordBox2.Clear();
            PasswordBox1.Focus();
            return;
        }

        var now = NoteDefaults.Now();
        var user = new UserLocal
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
            CreatedAt = now,
            UpdatedAt = now,
            IsAdmin = true,
            PasswordHash = SecurityHelper.Hash(password)
        };

        await _database.UpsertUserAsync(user);
        await _database.SetSettingAsync("current_user_id", user.Id);
        await _database.SetSettingAsync("current_author_id", user.Id);
        CompletedUserId = user.Id;
        App.AuthenticateAdminSession();
        DialogResult = true;
        Close();
    }
}
