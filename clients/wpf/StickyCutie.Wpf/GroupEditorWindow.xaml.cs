using System.Windows;

namespace StickyCutie.Wpf;

public partial class GroupEditorWindow : Window
{
    public string GroupName => NameTextBox.Text.Trim();
    public string? GroupDescription => string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? null : DescriptionTextBox.Text.Trim();

    public GroupEditorWindow(string? name = null, string? description = null)
    {
        InitializeComponent();
        NameTextBox.Text = name ?? string.Empty;
        DescriptionTextBox.Text = description ?? string.Empty;
        Loaded += (_, _) => NameTextBox.Focus();
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            MessageBox.Show("Informe o nome do grupo.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
