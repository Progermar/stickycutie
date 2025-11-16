using System;
using System.Globalization;
using System.Windows;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Alarms;

public partial class AlarmEditorWindow : Window
{
    readonly DatabaseService _database;
    readonly NoteLocal _note;
    AlarmLocal? _alarm;

    public AlarmEditorWindow(DatabaseService database, NoteLocal note, AlarmLocal? alarm)
    {
        _database = database;
        _note = note;
        _alarm = alarm;
        InitializeComponent();
        Loaded += (_, _) => InitializeFields();
    }

    void InitializeFields()
    {
        if (_alarm is { AlarmAt: > 0 })
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(_alarm.AlarmAt).LocalDateTime;
            DatePicker.SelectedDate = dt.Date;
            TimeTextBox.Text = dt.ToString("HH:mm");
        }
        RemoveButton.IsEnabled = _alarm != null && _alarm.IsEnabled;
    }

    async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DatePicker.SelectedDate == null)
        {
            MessageBox.Show("Escolha a data do alarme.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TimeSpan.TryParseExact(TimeTextBox.Text, "hh\\:mm", CultureInfo.InvariantCulture, out var time))
        {
            MessageBox.Show("Informe a hora no formato HH:mm.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var date = DatePicker.SelectedDate.Value.Date + time;
        if (date <= DateTime.Now)
        {
            MessageBox.Show("Escolha um horário no futuro.", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var now = NoteDefaults.Now();
        var alarm = _alarm ?? new AlarmLocal
        {
            Id = _alarm?.Id ?? Guid.NewGuid().ToString("N"),
            NoteId = _note.Id,
            CreatedAt = now
        };

        alarm.AlarmAt = new DateTimeOffset(date).ToUnixTimeSeconds();
        alarm.SnoozeUntil = null;
        alarm.IsEnabled = true;
        alarm.UpdatedAt = now;
        await _database.UpsertAlarmAsync(alarm);
        _alarm = alarm;
        AlarmManager.NotifyChange(_note.Id);
        DialogResult = true;
        Close();
    }

    async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_alarm == null)
        {
            DialogResult = true;
            Close();
            return;
        }

        var now = NoteDefaults.Now();
        _alarm.IsEnabled = false;
        _alarm.SnoozeUntil = null;
        _alarm.UpdatedAt = now;
        await _database.UpsertAlarmAsync(_alarm);
        AlarmManager.NotifyChange(_note.Id);
        DialogResult = true;
        Close();
    }
}
