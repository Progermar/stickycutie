using System.Windows;

namespace StickyCutie.Wpf;

public enum NoteCreationChoice
{
    None,
    Personal,
    OtherUser
}

public partial class CreateNoteChoiceWindow : Window
{
    public NoteCreationChoice Choice { get; private set; } = NoteCreationChoice.None;

    public CreateNoteChoiceWindow()
    {
        InitializeComponent();
    }

    void PersonalButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = NoteCreationChoice.Personal;
        DialogResult = true;
        Close();
    }

    void OtherButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = NoteCreationChoice.OtherUser;
        DialogResult = true;
        Close();
    }
}
