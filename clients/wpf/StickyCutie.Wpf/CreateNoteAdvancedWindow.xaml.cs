using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf;

public partial class CreateNoteAdvancedWindow : Window
{
    readonly DatabaseService _database;

    public string? SelectedUserId { get; private set; }
    public string? InitialText { get; private set; }
    public DateTimeOffset? AlarmDateTime { get; private set; }
    public string? NoteTitle { get; private set; }

    public CreateNoteAdvancedWindow(DatabaseService database)
    {
        _database = database;
        InitializeComponent();
        Loaded += async (_, _) => await LoadUsersAsync();
    }

    async Task LoadUsersAsync()
    {
        try
        {
            var users = await _database.GetUsersAsync();
            RecipientCombo.ItemsSource = users
                .OrderBy(u => u.Name)
                .ToList();
            RecipientCombo.SelectedItem = users.FirstOrDefault(u => u.Id == App.CurrentRecipientId)
                                          ?? users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível carregar os usuários: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecipientCombo.SelectedItem is not UserLocal recipient)
        {
            MessageBox.Show("Escolha um destinatário.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedUserId = recipient.Id;
        NoteTitle = string.IsNullOrWhiteSpace(TitleTextBox.Text) ? null : TitleTextBox.Text.Trim();
        InitialText = string.IsNullOrWhiteSpace(InitialTextBox.Text) ? null : InitialTextBox.Text.Trim();

        AlarmDateTime = null;
        if (AlarmDatePicker.SelectedDate.HasValue && !string.IsNullOrWhiteSpace(AlarmTimeBox.Text))
        {
            if (!TimeSpan.TryParse(AlarmTimeBox.Text, out var time))
            {
                MessageBox.Show("Hora do alarme inválida. Use HH:mm.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var date = AlarmDatePicker.SelectedDate.Value.Date + time;
            if (date <= DateTime.Now)
            {
                MessageBox.Show("Escolha um horário futuro para o alarme.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AlarmDateTime = new DateTimeOffset(date);
        }

        DialogResult = true;
        Close();
    }
}
