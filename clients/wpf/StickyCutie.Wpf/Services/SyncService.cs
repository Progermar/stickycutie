using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf.Services;

class SyncService
{
    readonly DatabaseService _database;
    readonly DispatcherTimer _timer;
    long _lastSync;
    bool _running;

    public SyncService(DatabaseService database)
    {
        _database = database;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _timer.Tick += async (_, _) => await ProcessAsync();
    }

    public async Task StartAsync()
    {
        _lastSync = await GetInitialSinceAsync();
        _timer.Start();
    }

    async Task<long> GetInitialSinceAsync()
    {
        var notes = await _database.GetNotesAsync();
        return notes.Count == 0 ? NoteDefaults.Now() : notes.Max(n => n.UpdatedAt);
    }

    async Task ProcessAsync()
    {
        if (_running) return;
        _running = true;
        try
        {
            var updates = await ApiService.GetUpdatesAsync(_lastSync);
            if (updates.Count == 0) return;

            var ackIds = new List<string>();
            foreach (var update in updates)
            {
                if (update.Note == null) continue;
                var applied = await ApplyRemoteNoteAsync(update.Note);
                _lastSync = Math.Max(_lastSync, update.Note.UpdatedAt);
                var eventId = string.IsNullOrWhiteSpace(update.EventId) ? update.Note.Id : update.EventId!;
                ackIds.Add(eventId);

                if (!applied.Deleted)
                {
                    Console.WriteLine($"[SYNC] Nota recebida: {applied.Title} ({applied.Id})");
                    Application.Current.Dispatcher.Invoke(() => App.Current.OpenNoteWindow(applied));
                }
                else
                {
                    var window = App.Current.GetNoteWindow(applied.Id);
                    window?.Close();
                }
            }

            if (ackIds.Count > 0)
            {
                var ackResult = await ApiService.AckAsync(ackIds);
                if (!ackResult)
                {
                    Console.WriteLine("[SYNC] Falha ao enviar ACK.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SYNC] Erro ao processar updates: {ex.Message}");
        }
        finally
        {
            _running = false;
        }
    }

    async Task<NoteLocal> ApplyRemoteNoteAsync(ApiService.RemoteNote remote)
    {
        var note = await _database.GetNoteAsync(remote.Id) ?? NoteDefaults.Create(
            remote.GroupId ?? App.CurrentGroupId,
            remote.TargetUserId ?? App.CurrentRecipientId,
            remote.CreatedByUserId ?? App.CurrentAuthorId);

        note.Id = remote.Id;
        note.Title = string.IsNullOrWhiteSpace(remote.Title) ? note.Title : remote.Title;
        note.Content = string.IsNullOrWhiteSpace(remote.Content) ? note.Content : remote.Content;
        note.UpdatedAt = remote.UpdatedAt == 0 ? NoteDefaults.Now() : remote.UpdatedAt;
        note.Deleted = remote.Deleted;
        note.SourceUserId = remote.TargetUserId ?? note.SourceUserId;
        note.CreatedByUserId = remote.CreatedByUserId ?? note.CreatedByUserId;
        note.GroupId = remote.GroupId ?? note.GroupId;

        await _database.UpsertNoteAsync(note);
        return note;
    }
}
