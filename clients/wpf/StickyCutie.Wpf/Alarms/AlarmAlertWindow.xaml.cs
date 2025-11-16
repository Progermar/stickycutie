using System;
using System.Windows;
using System.Windows.Documents;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Alarms;

public partial class AlarmAlertWindow : Window
{
    readonly AlarmLocal _alarm;
    readonly NoteLocal _note;

    public event EventHandler? StopRequested;
    public event EventHandler<int>? SnoozeRequested;

    public AlarmAlertWindow(NoteLocal note, AlarmLocal alarm)
    {
        _note = note;
        _alarm = alarm;
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(note.Title) ? "Nota" : note.Title;
        BodyPreview.Text = ExtractPreview(note.Content);
    }

    string ExtractPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Sem conteúdo";
        }

        try
        {
            var doc = System.Windows.Markup.XamlReader.Parse(content) as System.Windows.Documents.FlowDocument;
            var text = new System.Windows.Documents.TextRange(doc?.ContentStart ?? new System.Windows.Documents.FlowDocument().ContentStart, doc?.ContentEnd ?? new System.Windows.Documents.FlowDocument().ContentEnd).Text;
            text = text?.Trim() ?? string.Empty;
            return text.Length > 180 ? text[..180] + "..." : text;
        }
        catch
        {
            return "Conteúdo indisponível";
        }
    }

    void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AlarmSnoozeWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SnoozeRequested?.Invoke(this, dialog.SelectedMinutes);
            Close();
        }
    }
}
