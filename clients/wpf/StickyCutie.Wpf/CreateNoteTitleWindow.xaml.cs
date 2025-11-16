using System.Windows;

namespace StickyCutie.Wpf;

public partial class CreateNoteTitleWindow : Window
{
    public string? NoteTitle { get; private set; }

    public CreateNoteTitleWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TitleBox.Focus();
    }

    void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        NoteTitle = string.IsNullOrWhiteSpace(TitleBox.Text) ? null : TitleBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
