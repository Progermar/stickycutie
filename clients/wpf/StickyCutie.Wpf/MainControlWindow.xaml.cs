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
        var requireAuth = !string.IsNullOrWhiteSpace(App.CurrentGroupId);
        App.Current.ShowSettingsDialog(requireAuth);
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

