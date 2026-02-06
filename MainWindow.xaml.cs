using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Win11SlideshowPhotos;

public partial class MainWindow : Window
{
    private readonly Settings _settings;
    private readonly ImageCache _cache;
    private SlideShowQueue _queue = new(new List<List<string>>());
    private readonly DispatcherTimer _timer;
    private double _zoom = 1.0;
    private bool _isPaused;

    public MainWindow(string? argPath)
    {
        InitializeComponent();

        _settings = Settings.Load();
        _cache = new ImageCache(_settings.PreloadCount);
        _cache.ImageLoaded += OnImageLoaded;

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => NextImage();

        IntervalBox.Text = _settings.IntervalSeconds.ToString("0.00", CultureInfo.InvariantCulture);
        ApplyInterval(_settings.IntervalSeconds, autoStart: true);

        OpenFolderButton.Click += (_, _) => PickFolder();
        IntervalUpButton.Click += (_, _) => StepInterval(0.05);
        IntervalDownButton.Click += (_, _) => StepInterval(-0.05);
        IntervalBox.KeyDown += IntervalBox_KeyDown;
        IntervalBox.LostFocus += (_, _) => ApplyIntervalFromText();

        PreviewMouseWheel += OnPreviewMouseWheel;
        KeyDown += OnKeyDown;
        Closing += (_, _) => _settings.Save();

        ImageView.RenderTransform = new ScaleTransform(1.0, 1.0);

        var resolved = ResolveStartPath(argPath);
        if (resolved != null)
        {
            if (File.Exists(resolved))
            {
                _settings.RootFolder = Path.GetDirectoryName(resolved) ?? _settings.RootFolder;
                LoadQueue(_settings.RootFolder, resolved);
            }
            else if (Directory.Exists(resolved))
            {
                _settings.RootFolder = resolved;
                LoadQueue(_settings.RootFolder, null);
            }
        }
        else
        {
            LoadQueue(_settings.RootFolder, null);
        }

        ShowCurrent();
    }

    private static string? ResolveStartPath(string? argPath)
    {
        if (string.IsNullOrWhiteSpace(argPath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(argPath.Trim('"'));
        }
        catch
        {
            return null;
        }
    }

    private void PickFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        dialog.Description = "Choose a folder to play";
        dialog.UseDescriptionForTitle = true;
        dialog.SelectedPath = _settings.RootFolder;

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            _settings.RootFolder = dialog.SelectedPath;
            _settings.Save();
            LoadQueue(_settings.RootFolder, null);
            ShowCurrent();
        }
    }

    private void LoadQueue(string root, string? startPath)
    {
        _queue = SlideShowQueue.Build(root, startPath);
        _cache.Clear();

        if (_queue.IsEmpty)
        {
            StatusText.Text = "No images found. Choose another folder.";
            ImageView.Source = null;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            TogglePause();
            return;
        }

        if (e.Key == Key.Right)
        {
            NextImage();
        }
        else if (e.Key == Key.Left)
        {
            PrevImage();
        }
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
        }
        UpdateStatus();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var delta = e.Delta > 0 ? 0.1 : -0.1;
            _zoom = Math.Clamp(_zoom + delta, 0.1, 5.0);
            ApplyZoom();
        }
        else
        {
            if (e.Delta > 0)
            {
                PrevImage();
            }
            else
            {
                NextImage();
            }
        }
    }

    private void IntervalBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyIntervalFromText();
        }
    }

    private void StepInterval(double delta)
    {
        var value = ParseInterval(IntervalBox.Text, _settings.IntervalSeconds) + delta;
        ApplyInterval(value, autoStart: !_isPaused);
    }

    private void ApplyIntervalFromText()
    {
        var value = ParseInterval(IntervalBox.Text, _settings.IntervalSeconds);
        ApplyInterval(value, autoStart: !_isPaused);
    }

    private void ApplyInterval(double seconds, bool autoStart)
    {
        var clamped = Math.Clamp(seconds, 0.05, 60.0);
        _settings.IntervalSeconds = clamped;
        _settings.Save();
        IntervalBox.Text = clamped.ToString("0.00", CultureInfo.InvariantCulture);
        _timer.Interval = TimeSpan.FromSeconds(clamped);
        if (autoStart)
        {
            _timer.Start();
        }
    }

    private static double ParseInterval(string text, double fallback)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return fallback;
    }

    private void NextImage()
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        if (!_queue.TryNext())
        {
            _isPaused = true;
            _timer.Stop();
            UpdateStatus("End of folders");
            return;
        }

        ShowCurrent();
    }

    private void PrevImage()
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        if (_queue.TryPrev())
        {
            ShowCurrent();
        }
    }

    private void ShowCurrent()
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        RequestImages();
        RenderCurrent();
    }

    private void RequestImages()
    {
        var current = _queue.Current;
        var lookahead = _queue.ForwardPaths(_settings.PreloadCount);
        _cache.Request(new[] { current }.Concat(lookahead));
    }

    private void RenderCurrent()
    {
        var current = _queue.Current;
        var bitmap = _cache.Get(current);
        if (bitmap == null)
        {
            StatusText.Text = $"Loading: {Path.GetFileName(current)}";
            return;
        }

        ImageView.Source = bitmap;
        ApplyZoom();
        UpdateStatus();
    }

    private void UpdateStatus(string? overrideText = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            StatusText.Text = overrideText;
            return;
        }

        var status = $"{Path.GetFileName(_queue.Current)}  ({_queue.PositionText})";
        if (_isPaused)
        {
            status += "  [Paused]";
        }
        StatusText.Text = status;
    }

    private void ApplyZoom()
    {
        if (ImageView.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = _zoom;
            scale.ScaleY = _zoom;
        }
    }

    private void OnImageLoaded(string path)
    {
        Dispatcher.Invoke(() =>
        {
            if (_queue.IsEmpty)
            {
                return;
            }

            if (string.Equals(_queue.Current, path, StringComparison.OrdinalIgnoreCase))
            {
                RenderCurrent();
            }
        });
    }
}

internal sealed class Settings
{
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Win11SlideshowPhotos", "settings.json");

    public string RootFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public double IntervalSeconds { get; set; } = 2.0;

    public int PreloadCount { get; set; } = 6;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    if (string.IsNullOrWhiteSpace(settings.RootFolder))
                    {
                        settings.RootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    }
                    if (settings.IntervalSeconds <= 0)
                    {
                        settings.IntervalSeconds = 2.0;
                    }
                    return settings;
                }
            }
        }
        catch
        {
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
        }
    }
}

internal sealed class SlideShowQueue
{
    private static readonly string[] Extensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    ];

    private readonly List<List<string>> _groups;
    private int _groupIndex;
    private int _imageIndex;

    public SlideShowQueue(List<List<string>> groups)
    {
        _groups = groups;
        _groupIndex = 0;
        _imageIndex = 0;
    }

    public static SlideShowQueue Build(string root, string? startPath)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return new SlideShowQueue(new List<List<string>>());
        }

        var folderPaths = Directory.GetDirectories(root)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<List<string>>();
        if (folderPaths.Count == 0)
        {
            var images = CollectImages(root);
            if (images.Count > 0)
            {
                groups.Add(images);
            }
        }
        else
        {
            foreach (var folder in folderPaths)
            {
                var images = CollectImages(folder);
                if (images.Count > 0)
                {
                    groups.Add(images);
                }
            }
        }

        var queue = new SlideShowQueue(groups);
        if (!string.IsNullOrWhiteSpace(startPath))
        {
            queue.SetCurrentByPath(startPath);
        }
        return queue;
    }

    public bool IsEmpty => _groups.Count == 0;

    public string Current => _groups[_groupIndex][_imageIndex];

    public string PositionText => $"{_groupIndex + 1}/{_groups.Count} : {_imageIndex + 1}/{_groups[_groupIndex].Count}";

    public bool TryNext()
    {
        if (IsEmpty)
        {
            return false;
        }

        if (_imageIndex + 1 < _groups[_groupIndex].Count)
        {
            _imageIndex++;
            return true;
        }

        if (_groupIndex + 1 < _groups.Count)
        {
            _groupIndex++;
            _imageIndex = 0;
            return true;
        }

        return false;
    }

    public bool TryPrev()
    {
        if (IsEmpty)
        {
            return false;
        }

        if (_imageIndex > 0)
        {
            _imageIndex--;
            return true;
        }

        if (_groupIndex > 0)
        {
            _groupIndex--;
            _imageIndex = _groups[_groupIndex].Count - 1;
            return true;
        }

        return false;
    }

    public IEnumerable<string> ForwardPaths(int count)
    {
        if (IsEmpty || count <= 0)
        {
            yield break;
        }

        var groupIndex = _groupIndex;
        var imageIndex = _imageIndex;

        for (var i = 0; i < count; i++)
        {
            if (imageIndex + 1 < _groups[groupIndex].Count)
            {
                imageIndex++;
            }
            else if (groupIndex + 1 < _groups.Count)
            {
                groupIndex++;
                imageIndex = 0;
            }
            else
            {
                yield break;
            }

            yield return _groups[groupIndex][imageIndex];
        }
    }

    private void SetCurrentByPath(string path)
    {
        for (var gi = 0; gi < _groups.Count; gi++)
        {
            var group = _groups[gi];
            var index = group.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _groupIndex = gi;
                _imageIndex = index;
                return;
            }
        }
    }

    private static List<string> CollectImages(string folder)
    {
        return Directory.EnumerateFiles(folder)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal sealed class ImageCache
{
    private readonly int _maxItems;
    private readonly Dictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public ImageCache(int maxItems)
    {
        _maxItems = Math.Max(0, maxItems);
    }

    public event Action<string>? ImageLoaded;

    public void Request(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            lock (_lock)
            {
                if (_cache.ContainsKey(path) || _pending.Contains(path))
                {
                    continue;
                }

                _pending.Add(path);
            }

            _ = Task.Run(() => LoadAsync(path));
        }
    }

    public BitmapImage? Get(string path)
    {
        lock (_lock)
        {
            _cache.TryGetValue(path, out var image);
            return image;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _order.Clear();
            _pending.Clear();
        }
    }

    private void LoadAsync(string path)
    {
        BitmapImage? bitmap = null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            bitmap = image;
        }
        catch
        {
            bitmap = null;
        }

        lock (_lock)
        {
            _pending.Remove(path);
            if (bitmap != null)
            {
                _cache[path] = bitmap;
                _order.Enqueue(path);
                Trim();
            }
        }

        if (bitmap != null)
        {
            ImageLoaded?.Invoke(path);
        }
    }

    private void Trim()
    {
        while (_order.Count > _maxItems)
        {
            var old = _order.Dequeue();
            _cache.Remove(old);
        }
    }
}
