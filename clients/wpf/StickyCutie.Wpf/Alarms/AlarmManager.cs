using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Alarms;

sealed class AlarmManager
{
    readonly DatabaseService _database;
    readonly DispatcherTimer _timer;
    readonly HashSet<string> _activeAlerts = new();
    readonly MediaPlayer _mediaPlayer = new();
    readonly string[] _alarmSoundFolders;
    static string? _pendingCustomSound;
    string? _customSoundPath;
    bool _checking;

    public static event EventHandler<string>? AlarmStateChanged;

    static AlarmManager? _instance;

    AlarmManager(DatabaseService database)
    {
        _database = database;
        var localFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyCutie",
            "alarms");
        Directory.CreateDirectory(localFolder);
        var appFolder = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "Alarms");
        _alarmSoundFolders = new[] { localFolder, appFolder }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
        _ = LoadInitialSoundAsync();
        if (_pendingCustomSound != null)
        {
            _customSoundPath = _pendingCustomSound;
            _pendingCustomSound = null;
        }
    }

    public static void Initialize(DatabaseService database)
    {
        _instance ??= new AlarmManager(database);
    }

    public static void SetCustomSound(string? path)
    {
        if (_instance != null)
        {
            _instance._customSoundPath = path;
        }
        else
        {
            _pendingCustomSound = path;
        }
    }

    public static void NotifyChange(string noteId)
    {
        AlarmStateChanged?.Invoke(null, noteId);
    }

    async Task CheckAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var now = NoteDefaults.Now();
            var alarms = await _database.GetDueAlarmsAsync(now);
            foreach (var alarm in alarms)
            {
                if (_activeAlerts.Contains(alarm.Id)) continue;
                await ShowAlertAsync(alarm);
            }
        }
        finally
        {
            _checking = false;
        }
    }

    async Task ShowAlertAsync(AlarmLocal alarm)
    {
        if (!_activeAlerts.Add(alarm.Id))
        {
            return;
        }

        var note = await _database.GetNoteAsync(alarm.NoteId);
        if (note == null)
        {
            _activeAlerts.Remove(alarm.Id);
            return;
        }

        await ClearSnoozeAsync(alarm);
        PlayAlarmSound();

        var noteWindow = App.Current?.GetNoteWindow(alarm.NoteId);
        if (noteWindow != null)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                noteWindow.ShowAlarmInline(
                    async minutes =>
                    {
                        await SnoozeAlarmAsync(alarm, minutes);
                    },
                    async () =>
                    {
                        await StopAlarmAsync(alarm);
                    },
                    () => _activeAlerts.Remove(alarm.Id));
            });
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = new AlarmAlertWindow(note, alarm)
            {
                Owner = Application.Current.MainWindow
            };
            window.StopRequested += async (_, _) =>
            {
                await StopAlarmAsync(alarm);
                _activeAlerts.Remove(alarm.Id);
            };
            window.SnoozeRequested += async (_, minutes) =>
            {
                await SnoozeAlarmAsync(alarm, minutes);
                _activeAlerts.Remove(alarm.Id);
            };
            window.Closed += (_, _) => _activeAlerts.Remove(alarm.Id);
            window.Show();
        });
    }

    async Task StopAlarmAsync(AlarmLocal alarm)
    {
        alarm.IsEnabled = false;
        alarm.SnoozeUntil = null;
        alarm.UpdatedAt = NoteDefaults.Now();
        await _database.UpsertAlarmAsync(alarm);
        NotifyChange(alarm.NoteId);
    }

    async Task SnoozeAlarmAsync(AlarmLocal alarm, int minutes)
    {
        var interval = TimeSpan.FromMinutes(minutes);
        var now = NoteDefaults.Now();
        var seconds = Math.Max(1, (int)Math.Round(interval.TotalSeconds));
        var newDue = now + seconds;
        alarm.SnoozeUntil = newDue;
        alarm.AlarmAt = newDue;
        alarm.IsEnabled = true;
        alarm.UpdatedAt = now;
        await _database.UpsertAlarmAsync(alarm);
        NotifyChange(alarm.NoteId);
    }

    void PlayAlarmSound()
    {
        try
        {
            var custom = ResolveCustomSound();
            if (!string.IsNullOrEmpty(custom))
            {
                var ext = Path.GetExtension(custom).ToLowerInvariant();
                if (ext == ".wav")
                {
                    using var player = new SoundPlayer(custom);
                    player.Play();
                }
                else
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();
                    _mediaPlayer.Open(new Uri(custom, UriKind.Absolute));
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Volume = 1.0;
                    _mediaPlayer.Play();
                }
            }
            else
            {
                SystemSounds.Exclamation.Play();
            }
        }
        catch
        {
            SystemSounds.Exclamation.Play();
        }
    }

    string? ResolveCustomSound()
    {
        if (!string.IsNullOrWhiteSpace(_customSoundPath) && File.Exists(_customSoundPath))
        {
            return _customSoundPath;
        }

        foreach (var folder in _alarmSoundFolders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var preferred = new[] { "alarm.wav", "alarm.mp3" };
            foreach (var file in preferred)
            {
                var candidate = Path.Combine(folder, file);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var first = Directory.EnumerateFiles(folder)
                .FirstOrDefault(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".wav" or ".mp3" or ".wma" or ".aac";
                });
            if (first != null)
            {
                return first;
            }
        }

        return null;
    }

    async Task ClearSnoozeAsync(AlarmLocal alarm)
    {
        if (alarm.SnoozeUntil == null || alarm.SnoozeUntil == 0)
        {
            return;
        }

        alarm.SnoozeUntil = null;
        alarm.UpdatedAt = NoteDefaults.Now();
        await _database.UpsertAlarmAsync(alarm);
    }

    async Task LoadInitialSoundAsync()
    {
        try
        {
            _customSoundPath = await _database.GetSettingAsync("alarm_sound_path");
        }
        catch
        {
            _customSoundPath = null;
        }
    }
}
