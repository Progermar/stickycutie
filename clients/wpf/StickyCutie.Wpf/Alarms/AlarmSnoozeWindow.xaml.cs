using System;
using System.Windows;
using System.Windows.Controls;

namespace StickyCutie.Wpf.Alarms;

public partial class AlarmSnoozeWindow : Window
{
    public int SelectedMinutes { get; private set; } = 5;

    public AlarmSnoozeWindow()
    {
        InitializeComponent();
    }

    void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var customText = CustomMinutesBox.Text.Trim();
        var hasCustom = int.TryParse(customText, out var customValue) && customValue > 0;
        SelectedMinutes = 0;

        if (PresetCombo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
            {
                if (!hasCustom)
                {
                    MessageBox.Show("Informe os minutos para o adiar personalizado.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                SelectedMinutes = customValue;
            }
            else if (int.TryParse(tag, out var preset))
            {
                SelectedMinutes = preset;
            }
        }

        if (SelectedMinutes <= 0 && hasCustom)
        {
            SelectedMinutes = customValue;
        }

        if (SelectedMinutes <= 0)
        {
            MessageBox.Show("Selecione um intervalo ou informe minutos válidos.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
