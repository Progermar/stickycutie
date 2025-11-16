using System;
using StickyCutie.Wpf.Data;

namespace StickyCutie.Wpf;

static class NoteDefaults
{
    public const string BackgroundHex = "#FFF59A";
    public const string BorderHex = "#D4B445";
    public const int Width = 360;
    public const int Height = 300;
    public const int PositionX = 60;
    public const int PositionY = 60;
    public const string DefaultDocumentXaml =
        "<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph /></FlowDocument>";

    public static NoteLocal Create(string groupId, string sourceUserId, string createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("groupId is required", nameof(groupId));
        if (string.IsNullOrWhiteSpace(sourceUserId)) throw new ArgumentException("sourceUserId is required", nameof(sourceUserId));
        if (string.IsNullOrWhiteSpace(createdByUserId)) throw new ArgumentException("createdByUserId is required", nameof(createdByUserId));
        var now = Now();
        return new NoteLocal
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Nota",
            Content = DefaultDocumentXaml,
            Color = BackgroundHex,
            Theme = BorderHex,
            X = PositionX,
            Y = PositionY,
            Width = Width,
            Height = Height,
            Locked = false,
            LockPassword = null,
            AlarmEnabled = false,
            AlarmTime = 0,
            AlarmRepeat = null,
            PhotoMode = false,
            UpdatedAt = now,
            CreatedAt = now,
            Deleted = false,
            GroupId = groupId,
            SourceUserId = sourceUserId,
            CreatedByUserId = createdByUserId
        };
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
