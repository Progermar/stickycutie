using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;
using StickyCutie.Wpf.Data;
using StickyCutie.Wpf.Alarms;

namespace StickyCutie.Wpf;

public partial class MainWindow : Window
{
    readonly DatabaseService _database;
    readonly NoteLocal _note;
    readonly BrushConverter _brushConverter = new();
    readonly ContextMenu _noteContextMenu = new();
    readonly DispatcherTimer _contentSaveTimer;
    readonly DispatcherTimer _geometryTimer;
    readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    readonly Dictionary<string, NoteImage> _imageIndex = new();
    readonly TransformGroup _alarmTransformGroup = new();
    readonly TranslateTransform _shakeTransform = new();
    readonly RotateTransform _tiltTransform = new();
    AlarmLocal? _alarm;

    bool _isLocked;
    string? _lockPasswordHash;
    bool _pinOn = true;
    double _savedOpacityPercent = 12;
    string _currentBackgroundHex = NoteDefaults.BackgroundHex;
    string _currentBorderHex = NoteDefaults.BorderHex;
    bool _isPalettePopupHovered;
    bool _isPaletteButtonHovered;
    DependencyObject? _lastContextTarget;
    bool _suppressContentWatcher;
    bool _isInitializing = true;
    bool _contentDirty;
    bool _geometryDirty;
    int _nextImageOrderIndex;
    Func<int, Task>? _pendingAlarmSnooze;
    Func<Task>? _pendingAlarmStop;
    Action? _pendingAlarmDismiss;
    bool _forceTopmostDuringAlarm;

    static readonly string ImagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StickyCutie",
        "images");

    public MainWindow(NoteLocal note, DatabaseService database)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _lockPasswordHash = note.LockPassword;
        _isLocked = note.Locked;
        if (!string.IsNullOrWhiteSpace(note.Color)) _currentBackgroundHex = note.Color!;
        if (!string.IsNullOrWhiteSpace(note.Theme)) _currentBorderHex = note.Theme!;

        Directory.CreateDirectory(ImagesFolder);
        _alarmTransformGroup.Children.Add(_shakeTransform);
        _alarmTransformGroup.Children.Add(_tiltTransform);

        _contentSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _contentSaveTimer.Tick += async (_, _) => await PersistContentAsync();

        _geometryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _geometryTimer.Tick += async (_, _) => await PersistGeometryAsync();

        InitializeComponent();
        BuildNoteMenu();
        HookEvents();
        ApplyInitialState();
        _isInitializing = false;
        _ = LoadImagesMetadataAsync();
        _ = LoadAlarmStateAsync();
        AlarmManager.AlarmStateChanged += AlarmManager_AlarmStateChanged;
    }

    public string NoteId => _note.Id;

    protected override void OnClosing(CancelEventArgs e)
    {
        DismissAlarmInline();
        AlarmManager.AlarmStateChanged -= AlarmManager_AlarmStateChanged;
        _contentSaveTimer.Stop();
        _geometryTimer.Stop();
        try { PersistGeometryAsync().GetAwaiter().GetResult(); } catch { }
        try { PersistContentAsync().GetAwaiter().GetResult(); } catch { }
        base.OnClosing(e);
    }

    void HookEvents()
    {
        NoteRichText.SelectionChanged += NoteRichText_SelectionChanged;
        NoteRichText.ContextMenuOpening += NoteRichText_ContextMenuOpening;
        NoteRichText.PreviewMouseRightButtonDown += NoteRichText_PreviewMouseRightButtonDown;
        NoteRichText.TextChanged += NoteRichText_TextChanged;
        LockOverlay.MouseDown += LockOverlay_MouseDown;
        Loaded += OnLoaded;
        LocationChanged += WindowLocationChanged;
        SizeChanged += WindowSizeChanged;
    }

    void ApplyInitialState()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        ApplyNoteColor(_currentBackgroundHex, _currentBorderHex);

        _suppressContentWatcher = true;
        NoteRichText.Document = LoadDocument(_note.Content);
        _suppressContentWatcher = false;

        RestoreGeometry();
        _pinOn = true;
        SetLocked(_note.Locked, suppressPersist: true);
        UpdatePinVisualState();
        ApplyOpacityStyling();
    }

    void RestoreGeometry()
    {
        Width = _note.Width > 0 ? _note.Width : NoteDefaults.Width;
        Height = _note.Height > 0 ? _note.Height : NoteDefaults.Height;

        if (_note.X != 0 || _note.Y != 0)
        {
            Left = _note.X;
            Top = _note.Y;
        }
        else
        {
            Left = NoteDefaults.PositionX;
            Top = NoteDefaults.PositionY;
        }
    }

    static FlowDocument LoadDocument(string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                if (XamlReader.Parse(content) is FlowDocument parsed)
                {
                    return parsed;
                }
            }
            catch
            {
                // Reverte para documento vazio
            }
        }

        return CreateEmptyDocument();
    }

    static FlowDocument CreateEmptyDocument()
    {
        try
        {
            if (XamlReader.Parse(NoteDefaults.DefaultDocumentXaml) is FlowDocument parsed)
            {
                return parsed;
            }
        }
        catch
        {
            // Ignora falhas e volta para o layout padrao.
        }

        return new FlowDocument(new Paragraph(new Run()));
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Normal;
        Opacity = 1.0;
        Topmost = _pinOn;
        Activate();
        Focus();
    }

    void WindowLocationChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;
        _geometryDirty = true;
        _geometryTimer.Stop();
        _geometryTimer.Start();
    }

    void WindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isInitializing) return;
        _geometryDirty = true;
        _geometryTimer.Stop();
        _geometryTimer.Start();
    }

    void BuildNoteMenu()
    {
        NoteRichText.ContextMenu = _noteContextMenu;
    }

    void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (IsInside(NoteRichText, e.OriginalSource as DependencyObject)) return;
        DragMove();
    }

    static bool IsInside(DependencyObject ancestor, DependencyObject? node)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var choiceWindow = new CreateNoteChoiceWindow { Owner = this };
        if (choiceWindow.ShowDialog() != true) return;

        switch (choiceWindow.Choice)
        {
            case NoteCreationChoice.Personal:
                {
                    var titleWindow = new CreateNoteTitleWindow { Owner = this };
                    if (titleWindow.ShowDialog() == true)
                    {
                        await App.Current.CreatePersonalNoteAsync(titleWindow.NoteTitle);
                    }
                }
                break;
            case NoteCreationChoice.OtherUser:
                var advanced = new CreateNoteAdvancedWindow(_database) { Owner = this };
                if (advanced.ShowDialog() == true && !string.IsNullOrWhiteSpace(advanced.SelectedUserId))
                {
                    await App.Current.CreateNoteForRecipientAsync(
                        advanced.SelectedUserId!,
                        advanced.InitialText,
                        advanced.AlarmDateTime,
                        advanced.NoteTitle);
                }
                break;
        }
    }

    void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    void ToolbarArea_MouseEnter(object sender, MouseEventArgs e)
    {
        ToolbarContainer.Visibility = Visibility.Visible;
    }

    void ToolbarArea_MouseLeave(object sender, MouseEventArgs e)
    {
        if (TextColorPopup.IsOpen) return;
        if (NoteRichText.Selection.IsEmpty)
            ToolbarContainer.Visibility = Visibility.Collapsed;
    }

    void NoteRichText_SelectionChanged(object sender, RoutedEventArgs e)
    {
        ToolbarContainer.Visibility = NoteRichText.Selection.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        if (NoteRichText.Selection.IsEmpty)
        {
            _isPaletteButtonHovered = false;
            _isPalettePopupHovered = false;
            TextColorPopup.IsOpen = false;
        }
    }

    void NoteRichText_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastContextTarget = e.OriginalSource as DependencyObject;
    }

    void NoteRichText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressContentWatcher || _isInitializing) return;
        ScheduleContentSave();
    }

    void NoteRichText_ContextMenuOpening(object? sender, ContextMenuEventArgs e)
    {
        DependencyObject? target = _lastContextTarget;
        if (target == null)
        {
            var hit = NoteRichText.InputHitTest(Mouse.GetPosition(NoteRichText));
            target = hit as DependencyObject;
        }

        var imageContainer = FindImageContainer(target);
        _lastContextTarget = null;

        BuildContextMenu(imageContainer);
    }

    void BuildContextMenu(InlineUIContainer? imageContainer)
    {
        _noteContextMenu.Items.Clear();

        if (imageContainer != null)
        {
            var remove = new MenuItem { Header = "Excluir imagem" };
            remove.Click += (_, _) => RemoveImageContainer(imageContainer);
            _noteContextMenu.Items.Add(remove);
            return;
        }

        var hasSelection = !NoteRichText.Selection.IsEmpty;
        var canPaste = Clipboard.ContainsText() || Clipboard.ContainsImage();

        AddCommandMenuItem("Copiar", ApplicationCommands.Copy, hasSelection);
        AddCommandMenuItem("Recortar", ApplicationCommands.Cut, hasSelection);
        AddCommandMenuItem("Colar", ApplicationCommands.Paste, canPaste);
        AddCommandMenuItem("Desfazer", ApplicationCommands.Undo, ApplicationCommands.Undo.CanExecute(null, NoteRichText));
        AddCommandMenuItem("Refazer", ApplicationCommands.Redo, ApplicationCommands.Redo.CanExecute(null, NoteRichText));

        _noteContextMenu.Items.Add(new Separator());

        var newNote = new MenuItem { Header = "Nova nota" };
        newNote.Click += async (_, __) => await App.Current.CreateAndShowNoteAsync();
        _noteContextMenu.Items.Add(newNote);

        var close = new MenuItem { Header = "Fechar" };
        close.Click += (_, __) => Close();
        _noteContextMenu.Items.Add(close);

        var delete = new MenuItem { Header = "Excluir nota" };
        delete.Click += async (_, __) => await DeleteCurrentNoteAsync();
        _noteContextMenu.Items.Add(delete);
    }

    void AddCommandMenuItem(string header, ICommand command, bool canExecute)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            CommandTarget = NoteRichText,
            IsEnabled = canExecute
        };
        _noteContextMenu.Items.Add(item);
    }

    void PaletteButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPalettePopup();
    }

    void PaletteButton_MouseEnter(object sender, MouseEventArgs e)
    {
        _isPaletteButtonHovered = true;
        ShowPalettePopup();
    }

    void PaletteButton_MouseLeave(object sender, MouseEventArgs e)
    {
        _isPaletteButtonHovered = false;
        ClosePalettePopupIfIdleAsync();
    }

    void TextColorPopup_MouseEnter(object sender, MouseEventArgs e)
    {
        _isPalettePopupHovered = true;
    }

    void TextColorPopup_MouseLeave(object sender, MouseEventArgs e)
    {
        _isPalettePopupHovered = false;
        ClosePalettePopupIfIdleAsync();
    }

    void ShowPalettePopup()
    {
        TextColorPopup.PlacementTarget = PaletteButton;
        TextColorPopup.IsOpen = true;
    }

    async void ClosePalettePopupIfIdleAsync()
    {
        await Task.Delay(120);
        if (!_isPaletteButtonHovered && !_isPalettePopupHovered)
        {
            TextColorPopup.IsOpen = false;
        }
    }

    void TextColorButton_Click(object sender, RoutedEventArgs e) => ApplyHighlightColor(sender);

    void ApplyHighlightColor(object? sender)
    {
        if (NoteRichText.Selection.IsEmpty || sender is not Button button || button.Tag is not string hex) return;
        if (_brushConverter.ConvertFromString(hex) is Brush brush)
        {
            NoteRichText.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        }
    }

    void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        NoteMenuPopup.IsOpen = !NoteMenuPopup.IsOpen;
    }

    void NoteColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string payload) return;
        var parts = payload.Split('|');
        if (parts.Length != 2) return;
        ApplyNoteColor(parts[0], parts[1]);
        _note.Color = _currentBackgroundHex;
        _note.Theme = _currentBorderHex;
        NoteMenuPopup.IsOpen = false;
        _ = PersistNoteAsync();
    }

    void ListButton_Click(object sender, RoutedEventArgs e)
    {
        NoteRichText.Focus();
        Keyboard.Focus(NoteRichText);

        var caret = NoteRichText.CaretPosition;
        if (caret == null)
        {
            caret = NoteRichText.Document.ContentEnd;
        }

        var paragraph = caret.Paragraph;
        if (paragraph == null)
        {
            paragraph = new Paragraph(new Run());
            NoteRichText.Document.Blocks.Add(paragraph);
            NoteRichText.CaretPosition = paragraph.ContentStart;
        }

        if (NoteRichText.Selection.IsEmpty)
        {
            NoteRichText.Selection.Select(NoteRichText.CaretPosition, NoteRichText.CaretPosition);
        }

        if (EditingCommands.ToggleBullets.CanExecute(null, NoteRichText))
        {
            EditingCommands.ToggleBullets.Execute(null, NoteRichText);
        }
    }

    async void RemoveImageContainer(InlineUIContainer container)
    {
        if (container.Parent is Paragraph paragraph)
        {
            paragraph.Inlines.Remove(container);
        }

        var imageId = container.Tag as string;
        if (!string.IsNullOrEmpty(imageId))
        {
            try
            {
                await _database.DeleteImageAsync(imageId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível atualizar a imagem: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (_imageIndex.TryGetValue(imageId, out var noteImage))
            {
                _imageIndex.Remove(imageId);
                TryDeleteFile(noteImage.Path);
            }
        }

        ScheduleContentSave();
    }

    async void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
    {
        await DeleteCurrentNoteAsync();
    }

    async Task DeleteCurrentNoteAsync()
    {
        if (MessageBox.Show("Deseja excluir esta anotação?", "StickyCutie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _database.SoftDeleteNoteAsync(_note.Id, NoteDefaults.Now());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível excluir a anotação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Close();
    }
    void ScheduleContentSave()
    {
        _contentDirty = true;
        _contentSaveTimer.Stop();
        _contentSaveTimer.Start();
    }

    async Task PersistContentAsync()
    {
        _contentSaveTimer.Stop();
        if (!_contentDirty) return;
        _contentDirty = false;

        try
        {
            _note.Content = XamlWriter.Save(NoteRichText.Document);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível serializar a anotação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await PersistNoteAsync();
    }

    async Task PersistGeometryAsync()
    {
        _geometryTimer.Stop();
        if (!_geometryDirty) return;
        _geometryDirty = false;

        _note.X = (int)Math.Round(Left);
        _note.Y = (int)Math.Round(Top);
        _note.Width = (int)Math.Round(Width);
        _note.Height = (int)Math.Round(Height);

        await PersistNoteAsync();
    }

    async Task PersistNoteAsync()
    {
        _note.UpdatedAt = NoteDefaults.Now();
        await _saveSemaphore.WaitAsync();
        try
        {
            await _database.UpsertNoteAsync(_note);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }
    void ApplyNoteColor(string backgroundHex, string borderHex)
    {
        _currentBackgroundHex = backgroundHex;
        _currentBorderHex = borderHex;

        if (_brushConverter.ConvertFromString(backgroundHex) is Brush background)
        {
            RootBorder.Background = background;
        }

        if (_brushConverter.ConvertFromString(borderHex) is Brush border)
        {
            RootBorder.BorderBrush = border;
        }
    }

    void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _pinOn = !_pinOn;
        ApplyPinState();
        PinOnIcon.Visibility = _pinOn ? Visibility.Visible : Visibility.Collapsed;
        PinOffIcon.Visibility = _pinOn ? Visibility.Collapsed : Visibility.Visible;
        ApplyOpacityStyling();
    }

    void ApplyPinState()
    {
        RootBorder.Opacity = _pinOn ? 1.0 : (_savedOpacityPercent / 100.0);
        Topmost = true;
    }

    void DragZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    async void ImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar imagem",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"
        };

        if (dialog.ShowDialog() == true)
        {
            await InsertImageAsync(dialog.FileName);
        }
    }

    async Task InsertImageAsync(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            var imageId = Guid.NewGuid().ToString("N");
            var targetPath = Path.Combine(ImagesFolder, $"{imageId}.png");

            using (var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }

            var caret = NoteRichText.CaretPosition ?? NoteRichText.Document.ContentEnd;
            if (!caret.IsAtInsertionPosition)
            {
                caret = caret.GetInsertionPosition(LogicalDirection.Forward) ?? caret;
            }

            var image = new Image
            {
                Source = new BitmapImage(new Uri(targetPath)),
                Stretch = Stretch.Uniform,
                Width = Math.Min(220, bitmap.PixelWidth),
                Margin = new Thickness(0, 8, 0, 8)
            };

            _ = new InlineUIContainer(image, caret)
            {
                Tag = imageId
            };

            var noteImage = new NoteImage
            {
                Id = imageId,
                NoteId = _note.Id,
                Path = targetPath,
                OrderIndex = _nextImageOrderIndex++,
                Duration = 0,
                CreatedAt = NoteDefaults.Now()
            };

            await _database.UpsertNoteImageAsync(noteImage);
            _imageIndex[imageId] = noteImage;

            NoteRichText.Focus();
            ScheduleContentSave();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível inserir a imagem: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    async void AlarmButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new AlarmEditorWindow(_database, _note, _alarm);
        editor.Owner = this;
        if (editor.ShowDialog() == true)
        {
            await LoadAlarmStateAsync();
        }
    }

    public void ShowAlarmInline(Func<int, Task> snoozeAction, Func<Task> stopAction, Action dismissCallback)
    {
        _pendingAlarmSnooze = snoozeAction;
        _pendingAlarmStop = stopAction;
        _pendingAlarmDismiss = dismissCallback;
        AlarmInlineOverlay.Visibility = Visibility.Visible;
        StartShakeAnimation();
        BringAlarmToFront();
    }

    public void DismissAlarmInline()
    {
        AlarmInlineOverlay.Visibility = Visibility.Collapsed;
        StopShakeAnimation();
        _pendingAlarmSnooze = null;
        _pendingAlarmStop = null;
        _pendingAlarmDismiss?.Invoke();
        _pendingAlarmDismiss = null;
        if (_forceTopmostDuringAlarm && !_pinOn)
        {
            Topmost = false;
        }
        _forceTopmostDuringAlarm = false;
    }

    void BringAlarmToFront()
    {
        Activate();
        if (!_pinOn)
        {
            Topmost = true;
            _forceTopmostDuringAlarm = true;
        }
    }

    void StartShakeAnimation()
    {
        if (!ReferenceEquals(RootBorder.RenderTransform, _alarmTransformGroup))
        {
            RootBorder.RenderTransform = _alarmTransformGroup;
        }

        var translate = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(360)
        };
        translate.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        translate.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        translate.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        _shakeTransform.BeginAnimation(TranslateTransform.XProperty, translate);

        var rotate = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(360)
        };
        rotate.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        rotate.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        rotate.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        _tiltTransform.BeginAnimation(RotateTransform.AngleProperty, rotate);
    }

    void StopShakeAnimation()
    {
        _shakeTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _shakeTransform.X = 0;
        _tiltTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        _tiltTransform.Angle = 0;
    }

    async void AlarmInlineSnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingAlarmSnooze == null)
        {
            DismissAlarmInline();
            return;
        }

        var dialog = new AlarmSnoozeWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _pendingAlarmSnooze(dialog.SelectedMinutes);
                DismissAlarmInline();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível adiar o alarme: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    async void AlarmInlineStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingAlarmStop == null)
        {
            DismissAlarmInline();
            return;
        }

        try
        {
            await _pendingAlarmStop();
            DismissAlarmInline();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível encerrar o alarme: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void LockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLocked)
        {
            PasswordModalTitle.Text = "Desbloquear";
            PasswordBox2.Visibility = Visibility.Collapsed;
            PasswordModal.Visibility = Visibility.Visible;
            PasswordBox1.Password = string.Empty;
            PasswordBox2.Password = string.Empty;
            PasswordConfirm.Tag = "unlock";
            PasswordRemove.Visibility = string.IsNullOrEmpty(_lockPasswordHash) ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        if (string.IsNullOrEmpty(_lockPasswordHash))
        {
            PasswordModalTitle.Text = "Definir senha";
            PasswordBox2.Visibility = Visibility.Visible;
            PasswordModal.Visibility = Visibility.Visible;
            PasswordBox1.Password = string.Empty;
            PasswordBox2.Password = string.Empty;
            PasswordConfirm.Tag = "set";
            PasswordRemove.Visibility = Visibility.Collapsed;
        }
        else
        {
            PasswordModalTitle.Text = "Desbloquear";
            PasswordBox2.Visibility = Visibility.Collapsed;
            PasswordModal.Visibility = Visibility.Visible;
            PasswordBox1.Password = string.Empty;
            PasswordBox2.Password = string.Empty;
            PasswordConfirm.Tag = "unlock";
            PasswordRemove.Visibility = Visibility.Visible;
        }
    }

    void PasswordCancel_Click(object sender, RoutedEventArgs e)
    {
        PasswordModal.Visibility = Visibility.Collapsed;
    }

    static string Hash(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    void PasswordConfirm_Click(object sender, RoutedEventArgs e)
    {
        var mode = PasswordConfirm.Tag as string;
        if (mode == "set")
        {
            if (PasswordBox1.Password.Length > 0 && PasswordBox1.Password == PasswordBox2.Password)
            {
                _lockPasswordHash = Hash(PasswordBox1.Password);
                _note.LockPassword = _lockPasswordHash;
                SetLocked(true);
                PasswordModal.Visibility = Visibility.Collapsed;
                PasswordBox1.Password = string.Empty;
                PasswordBox2.Password = string.Empty;
            }
        }
        else if (mode == "unlock")
        {
            if (!string.IsNullOrEmpty(_lockPasswordHash) && Hash(PasswordBox1.Password) == _lockPasswordHash)
            {
                SetLocked(false);
                PasswordModal.Visibility = Visibility.Collapsed;
                PasswordBox1.Password = string.Empty;
            }
            else
            {
                PasswordBox1.Password = string.Empty;
            }
        }
    }
    void PasswordRemove_Click(object sender, RoutedEventArgs e)
    {
        var entered = PasswordBox1.Password;
        if (!string.IsNullOrEmpty(_lockPasswordHash) && Hash(entered) == _lockPasswordHash)
        {
            _lockPasswordHash = null;
            _note.LockPassword = null;
            SetLocked(false);
            PasswordModal.Visibility = Visibility.Collapsed;
            PasswordBox1.Password = string.Empty;
            PasswordBox2.Password = string.Empty;
            MessageBox.Show("Senha removida com sucesso.");
        }
        else
        {
            MessageBox.Show("Senha incorreta. Digite a senha atual para remover.");
            PasswordBox1.Password = string.Empty;
        }
    }
    void SetLocked(bool locked, bool suppressPersist = false)
    {
        _isLocked = locked;
        _note.Locked = locked;
        NoteRichText.Visibility = locked ? Visibility.Hidden : Visibility.Visible;
        LockOverlay.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        if (LockClosedIcon != null && LockOpenIcon != null)
        {
            LockClosedIcon.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
            LockOpenIcon.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
        }

        if (!suppressPersist && !_isInitializing)
        {
            _ = PersistNoteAsync();
        }
    }

    void LockOverlay_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        if (_isLocked)
        {
            PasswordModalTitle.Text = "Desbloquear";
            PasswordBox2.Visibility = Visibility.Collapsed;
            PasswordModal.Visibility = Visibility.Visible;
            PasswordBox1.Password = string.Empty;
            PasswordBox2.Password = string.Empty;
            PasswordConfirm.Tag = "unlock";
            PasswordRemove.Visibility = string.IsNullOrEmpty(_lockPasswordHash) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    

    

    void UpdatePinVisualState()
    {
        if (PinButton == null) return;
        PinButton.Background = _pinOn ? new SolidColorBrush(Color.FromArgb(255, 255, 239, 173)) : Brushes.Transparent;
        PinButton.BorderBrush = _pinOn ? new SolidColorBrush(Color.FromArgb(255, 231, 193, 96)) : Brushes.Transparent;
    }

    void ApplyOpacityStyling()
    {
        RootBorder.BorderThickness = !_pinOn ? new Thickness(0) : new Thickness(1);
        PinButton.Opacity = 1.0;
    }

    static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignora falhas silenciosamente
        }
    }

    async Task LoadImagesMetadataAsync()
    {
        try
        {
            var images = await _database.GetImagesAsync(_note.Id);
            foreach (var image in images)
            {
                _imageIndex[image.Id] = image;
            }

            _nextImageOrderIndex = images.Count > 0 ? images.Max(i => i.OrderIndex) + 1 : 0;

            foreach (var container in EnumerateImageContainers())
            {
                if (container.Child is not Image imageControl) continue;
                var id = container.Tag as string;
                if (!string.IsNullOrEmpty(id))
                {
                    if (!_imageIndex.ContainsKey(id))
                    {
                        var recreated = await RegisterInlineImageAsync(imageControl, id);
                        if (recreated != null)
                        {
                            _imageIndex[id] = recreated;
                        }
                    }
                }
                else
                {
                    var recreated = await RegisterInlineImageAsync(imageControl);
                    if (recreated != null)
                    {
                        container.Tag = recreated.Id;
                        _imageIndex[recreated.Id] = recreated;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao carregar imagens: {ex.Message}", "StickyCutie", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    IEnumerable<InlineUIContainer> EnumerateImageContainers()
    {
        if (NoteRichText?.Document == null) yield break;

        foreach (var block in NoteRichText.Document.Blocks)
        {
            foreach (var container in EnumerateImageContainers(block))
            {
                yield return container;
            }
        }
    }

    IEnumerable<InlineUIContainer> EnumerateImageContainers(Block block)
    {
        switch (block)
        {
            case Paragraph paragraph:
                foreach (var inline in paragraph.Inlines)
                {
                    if (inline is InlineUIContainer container && container.Child is Image)
                    {
                        yield return container;
                    }
                }
                break;
            case Section section:
                foreach (var nested in EnumerateImageContainers(section.Blocks))
                {
                    yield return nested;
                }
                break;
            case System.Windows.Documents.List list:
                foreach (System.Windows.Documents.ListItem item in list.ListItems)
                {
                    foreach (var nested in EnumerateImageContainers(item.Blocks))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }

    IEnumerable<InlineUIContainer> EnumerateImageContainers(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var nested in EnumerateImageContainers(block))
            {
                yield return nested;
            }
        }
    }

    async Task<NoteImage?> RegisterInlineImageAsync(Image image, string? forcedId = null)
    {
        var uri = GetImageSourceUri(image);
        if (uri == null) return null;
        var path = uri.IsAbsoluteUri ? (uri.IsFile ? uri.LocalPath : uri.ToString()) : uri.ToString();

        var noteImage = new NoteImage
        {
            Id = forcedId ?? Guid.NewGuid().ToString("N"),
            NoteId = _note.Id,
            Path = path,
            OrderIndex = _nextImageOrderIndex++,
            Duration = 0,
            CreatedAt = NoteDefaults.Now()
        };

        await _database.UpsertNoteImageAsync(noteImage);
        return noteImage;
    }

    static Uri? GetImageSourceUri(Image image)
    {
        if (image.Source is BitmapImage bmp && bmp.UriSource != null)
        {
            return bmp.UriSource;
        }

        return null;
    }
    static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = current switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(current),
                FrameworkContentElement fce => fce.Parent ?? fce.TemplatedParent,
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }

    static InlineUIContainer? FindImageContainer(DependencyObject? source)
    {
        if (source is Image img)
        {
            if (img.Tag is InlineUIContainer tagged)
            {
                return tagged;
            }

            return FindAncestor<InlineUIContainer>(img);
        }

        var container = FindAncestor<InlineUIContainer>(source);
        if (container?.Child is Image)
        {
            return container;
        }

        if (source is FrameworkContentElement fce)
        {
            return FindImageContainer(fce.Parent);
        }

        return null;
    }

    async Task LoadAlarmStateAsync()
    {
        _alarm = await _database.GetAlarmAsync(_note.Id);
        UpdateAlarmVisualState();
    }

    void UpdateAlarmVisualState()
    {
        if (AlarmBadge == null) return;
        AlarmBadge.Visibility = _alarm?.IsEnabled == true ? Visibility.Visible : Visibility.Collapsed;
    }

    void AlarmManager_AlarmStateChanged(object? sender, string noteId)
    {
        if (!string.Equals(noteId, _note.Id, StringComparison.OrdinalIgnoreCase)) return;
        Dispatcher.Invoke(async () => await LoadAlarmStateAsync());
    }
}
