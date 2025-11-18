using System;
using System.Net.Mail;
using System.Text;
using System.Windows;
using StickyCutie.Wpf.Data;
using StickyCutie.Wpf.Services;

namespace StickyCutie.Wpf;

public partial class JoinGroupWindow : Window
{
    bool _validating;

    public JoinGroupWindow(string? defaultName = null, string? defaultEmail = null)
    {
        InitializeComponent();
        NameTextBox.Text = defaultName ?? string.Empty;
        EmailTextBox.Text = defaultEmail ?? string.Empty;
    }

    async void JoinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_validating) return;
        var token = TokenTextBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Informe o código do convite.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            TokenTextBox.Focus();
            return;
        }

        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Informe seu nome.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            NameTextBox.Focus();
            return;
        }

        var email = EmailTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            MessageBox.Show("Informe seu e-mail.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            EmailTextBox.Focus();
            return;
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            MessageBox.Show("Informe um e-mail válido.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _validating = true;
            PreviewText.Text = "Validando convite...";
            var preview = await ApiService.GetInvitePreviewAsync(token);
            if (!string.Equals(preview.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Este convite já foi utilizado ou revogado.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Grupo: {preview.GroupName}");
            sb.AppendLine($"Expira em: {preview.ExpiresAt:dd/MM HH:mm}");
            PreviewText.Text = sb.ToString();

            var result = await ApiService.AcceptInviteAsync(token, name, email, PhoneTextBox.Text.Trim());

            await App.Current.ApplyJoinedContextAsync(result.Group, result.User, result.AccessToken);

            MessageBox.Show($"Convite aceito! Você agora faz parte do grupo {preview.GroupName}.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível aceitar o convite: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _validating = false;
        }
    }
}
