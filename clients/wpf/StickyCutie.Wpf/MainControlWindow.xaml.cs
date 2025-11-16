using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace StickyCutie.Wpf;

public partial class MainControlWindow : Window
{
    public MainControlWindow()
    {
        InitializeComponent();
    }

    void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!App.IsCurrentAuthorAdmin)
        {
            MessageBox.Show("Somente usuários administradores podem abrir as configurações globais. Peça ao administrador atual para conceder acesso.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!App.IsAdminSessionAuthenticated)
        {
            var auth = new AdminPasswordWindow
            {
                Owner = this
            };

            if (auth.ShowDialog() != true)
            {
                return;
            }
        }

        var settings = new SettingsWindow();
        settings.Owner = this;
        settings.ShowDialog();
        App.InvalidateAdminSession();
    }

    void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

