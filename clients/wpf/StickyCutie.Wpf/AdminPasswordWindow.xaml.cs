using System.Windows;

namespace StickyCutie.Wpf;

public partial class AdminPasswordWindow : Window
{
    public AdminPasswordWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => AdminPasswordBox.Focus();
    }

    void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var password = AdminPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Informe a senha do administrador.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var hash = SecurityHelper.Hash(password);
        if (!string.IsNullOrEmpty(App.CurrentAuthorPasswordHash) && hash == App.CurrentAuthorPasswordHash)
        {
            App.AuthenticateAdminSession();
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Senha incorreta.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
            AdminPasswordBox.Clear();
            AdminPasswordBox.Focus();
        }
    }
}

