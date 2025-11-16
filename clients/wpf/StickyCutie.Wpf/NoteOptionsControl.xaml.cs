using System;
using System.Windows;
using System.Windows.Controls;

namespace StickyCutie.Wpf;

public partial class NoteOptionsControl : UserControl
{
    public event EventHandler<(string bg, string border)>? ColorSelected;

    DateTime _createdAt = DateTime.Now;
    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            _createdAt = value;
            CreatedText.Text = $"Created on {value.ToString("MM/dd/yy, h:mm tt")}";
        }
    }

    public NoteOptionsControl()
    {
        InitializeComponent();
        CreatedText.Text = $"Created on {CreatedAt.ToString("MM/dd/yy, h:mm tt")}";
    }

    void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string t)
        {
            var parts = t.Split('|');
            if (parts.Length == 2)
                ColorSelected?.Invoke(this, (parts[0], parts[1]));
        }
    }
}