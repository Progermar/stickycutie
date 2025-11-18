using System.Net.Mail;
using System.Windows;
using StickyCutie.Wpf.Services;

namespace StickyCutie.Wpf;

public partial class InviteDialogWindow : Window
{
    readonly string _groupId;

    public ApiService.GroupInviteDto? Result { get; private set; }

    public InviteDialogWindow(string groupId)
    {
        _groupId = groupId;
        InitializeComponent();
    }

    async void GenerateInvite_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(email))
        {
            try
            {
                _ = new MailAddress(email);
            }
            catch
            {
                MessageBox.Show("Informe um e-mail válido ou deixe o campo em branco.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                EmailTextBox.Focus();
                return;
            }
        }

        try
        {
            var invite = await ApiService.CreateInviteAsync(_groupId, email);
            Result = invite;
            ResultText.Text = $"Convite gerado! Token: {invite.Token}\nExpira em: {invite.ExpiresAt:dd/MM HH:mm}";
            Clipboard.SetText(invite.Token);
            MessageBox.Show($"Token copiado para a área de transferência:\n{invite.Token}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Não foi possível gerar o convite: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
