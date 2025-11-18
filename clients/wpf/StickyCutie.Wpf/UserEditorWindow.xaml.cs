using System.Windows;

namespace StickyCutie.Wpf;

public partial class UserEditorWindow : Window
{
    readonly bool _requirePassword;
    readonly bool _allowAdminToggle;
    string? _passwordHash;
    public string? PlainPassword { get; private set; }

    public string UserName => NameTextBox.Text.Trim();
    public string UserEmail => EmailTextBox.Text.Trim();
    public string? UserPhone => string.IsNullOrWhiteSpace(PhoneTextBox.Text) ? null : PhoneTextBox.Text.Trim();
    public bool IsAdmin => AdminCheckBox.IsChecked == true;
    public string? PasswordHash => _passwordHash;

    public UserEditorWindow(bool requirePassword, bool allowAdminToggle = true, string? name = null, string? email = null, string? phone = null, bool isAdmin = false, string? existingPasswordHash = null, string? title = null)
    {
        _requirePassword = requirePassword;
        _allowAdminToggle = allowAdminToggle;
        _passwordHash = existingPasswordHash;

        InitializeComponent();
        Title = title ?? (name == null ? "Novo usuário" : "Editar usuário");
        NameTextBox.Text = name ?? string.Empty;
        EmailTextBox.Text = email ?? string.Empty;
        PhoneTextBox.Text = phone ?? string.Empty;
        AdminCheckBox.IsChecked = isAdmin;
        AdminCheckBox.Visibility = allowAdminToggle ? Visibility.Visible : Visibility.Collapsed;
        PasswordLabel.Text = requirePassword ? "Senha" : "Nova senha (opcional)";
        PasswordConfirmLabel.Text = requirePassword ? "Confirmar senha" : "Confirmar nova senha";
        Loaded += (_, _) => NameTextBox.Focus();
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(UserEmail))
        {
            MessageBox.Show("Informe nome e e-mail.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var password = PasswordBox.Password;
        var confirm = PasswordConfirmBox.Password;

        if (_requirePassword || !string.IsNullOrWhiteSpace(password) || !string.IsNullOrWhiteSpace(confirm))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Informe a senha.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("A confirmação de senha não confere.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Clear();
                PasswordConfirmBox.Clear();
                PasswordBox.Focus();
                return;
            }

            PlainPassword = password;
            _passwordHash = SecurityHelper.Hash(password);
        }

        if (_requirePassword && string.IsNullOrEmpty(_passwordHash))
        {
            MessageBox.Show("Defina uma senha para o usuário.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
