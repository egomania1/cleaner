using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Clean.App.Controls;
using Clean.Core.Apps;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Clean.App.ViewModels;

public sealed class AppsViewModel : ObservableObject
{
    public const int LiveCapacity = 60;
    public const double IntervalSeconds = 1.5;

    private const int TreemapTileCount = 32;
    private const int RunningRowCount = 12;
    private const int ParallelMeasures = 3;
    private const string OthersKey = "others";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IInstalledProgramCatalog _catalog;
    private readonly IFolderSizer _folderSizer;
    private readonly IProcessMonitor _monitor;
    private readonly IFileExplorer _explorer;
    private readonly ILogger<AppsViewModel> _logger;
    private readonly Dictionary<string, AppItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _memoryHistory = new();

    private Task? _loading;
    private DispatcherQueueTimer? _timer;
    private UsageCalculator? _calculator;
    private IReadOnlyList<ProcessSample> _previousSamples = [];
    private DateTime _previousSampleTime;
    private bool _isSampling;
    private bool _sizesChanged;
    private int _measuredCount;
    private int _toMeasureCount;
    private string _searchText = string.Empty;
    private int _sortIndex;
    private bool _sortRunningByMemory;
    private AppItem? _selectedApp;
    private IReadOnlyList<AppItem> _filteredApps = [];
    private IReadOnlyList<TreemapTile> _tiles = [];
    private IReadOnlyList<GroupShareRow> _groups = [];
    private double _totalCpu;
    private long _totalMemory;
    private int _runningCount;
    private int _otherRunningCount;
    private bool _isLoaded;

    public AppsViewModel(
        IInstalledProgramCatalog catalog,
        IFolderSizer folderSizer,
        IProcessMonitor monitor,
        IFileExplorer explorer,
        ILogger<AppsViewModel> logger)
    {
        _catalog = catalog;
        _folderSizer = folderSizer;
        _monitor = monitor;
        _explorer = explorer;
        _logger = logger;
        OpenLocationCommand = new RelayCommand(() => _explorer.Reveal(SelectedApp!.LocationText!), () => SelectedApp?.HasLocation == true);
        OpenAppsSettingsCommand = new AsyncRelayCommand(async () => await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures")));
        SortRunningByCpuCommand = new RelayCommand(() => SortRunningByMemory = false);
        SortRunningByMemoryCommand = new RelayCommand(() => SortRunningByMemory = true);
    }

    public ICommand OpenLocationCommand { get; }

    public ICommand OpenAppsSettingsCommand { get; }

    public ICommand SortRunningByCpuCommand { get; }

    public ICommand SortRunningByMemoryCommand { get; }

    public ObservableCollection<AppItem> RunningApps { get; } = [];

    public IReadOnlyList<string> SortOptions { get; } = ["Taille", "Nom", "Date d'installation", "En cours d'abord"];

    public bool IsLoaded
    {
        get => _isLoaded;
        private set
        {
            if (SetProperty(ref _isLoaded, value))
            {
                OnPropertyChanged(nameof(IsLoading));
            }
        }
    }

    public bool IsLoading => !IsLoaded;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshFilter();
            }
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (SetProperty(ref _sortIndex, value))
            {
                RefreshFilter();
            }
        }
    }

    public bool SortRunningByMemory
    {
        get => _sortRunningByMemory;
        private set
        {
            if (SetProperty(ref _sortRunningByMemory, value))
            {
                OnPropertyChanged(nameof(RunningSortText));
                SyncRunning();
            }
        }
    }

    public string RunningSortText => SortRunningByMemory ? "Triées par mémoire" : "Triées par processeur";

    public AppItem? SelectedApp
    {
        get => _selectedApp;
        private set
        {
            if (SetProperty(ref _selectedApp, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasNoSelection));
                OnPropertyChanged(nameof(SelectedKey));
                ((RelayCommand)OpenLocationCommand).NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedApp is not null;

    public bool HasNoSelection => SelectedApp is null;

    public string? SelectedKey => SelectedApp?.Key;

    public IReadOnlyList<AppItem> FilteredApps
    {
        get => _filteredApps;
        private set
        {
            if (SetProperty(ref _filteredApps, value))
            {
                OnPropertyChanged(nameof(FilteredCountText));
            }
        }
    }

    public string FilteredCountText => FilteredApps.Count == Installed.Count()
        ? $"{FilteredApps.Count} applications"
        : $"{FilteredApps.Count} sur {Installed.Count()} applications";

    public IReadOnlyList<TreemapTile> Tiles
    {
        get => _tiles;
        private set => SetProperty(ref _tiles, value);
    }

    public IReadOnlyList<GroupShareRow> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public string InstalledCountText => Installed.Count().ToString("N0", French);

    public string TotalSizeText => ByteSize.Format(Installed.Sum(item => item.SizeBytes));

    public string TotalSizeCaption => _measuredCount < _toMeasureCount
        ? $"Mesure des dossiers : {_measuredCount} / {_toMeasureCount}"
        : "Mesuré sur le disque";

    public double MeasureProgress => _toMeasureCount == 0 ? 1 : (double)_measuredCount / _toMeasureCount;

    public bool IsMeasuring => _measuredCount < _toMeasureCount;

    public string RunningCountText => _runningCount.ToString("N0", French);

    public string CpuText => $"{_totalCpu.ToString(_totalCpu < 10 ? "0.0" : "0", French)} %";

    public string CpuCaption => $"{_monitor.ProcessorCount} cœurs logiques";

    public string MemoryText => ByteSize.Format(_totalMemory);

    public string MemoryCaption => $"sur {ByteSize.Format(_monitor.TotalMemoryBytes)} de mémoire vive";

    public double MemoryRatio => _monitor.TotalMemoryBytes > 0 ? Math.Clamp((double)_totalMemory / _monitor.TotalMemoryBytes, 0, 1) : 0;

    public double MemoryMaximum => _monitor.TotalMemoryBytes;

    public IReadOnlyList<double> CpuHistory => _cpuHistory.ToList();

    public IReadOnlyList<double> MemoryHistory => _memoryHistory.ToList();

    public string OtherRunningText => _otherRunningCount > 0
        ? $"+ {_otherRunningCount} autre{(_otherRunningCount > 1 ? "s" : string.Empty)} programme{(_otherRunningCount > 1 ? "s" : string.Empty)} en cours, moins actif{(_otherRunningCount > 1 ? "s" : string.Empty)}"
        : string.Empty;

    private IEnumerable<AppItem> Installed => _items.Values.Where(item => item.IsInstalled);

    public Task EnsureLoadedAsync() => _loading ??= LoadAsync();

    public void StartMonitoring()
    {
        if (_timer is null)
        {
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
            _timer.Tick += async (_, _) => await SampleAsync();
        }

        _timer.Start();
        _ = SampleAsync();
    }

    // Sampling ~300 processes costs a little CPU; it only runs while the page is on screen.
    public void StopMonitoring() => _timer?.Stop();

    public void Select(string? key)
    {
        SelectedApp = key is not null && key != OthersKey && _items.TryGetValue(key, out var item) ? item : null;
    }

    private async Task LoadAsync()
    {
        try
        {
            var shared = SharedFolders();
            var programs = await Task.Run(() => _catalog.All);
            var inventory = AppInventory.Build(programs, shared);
            _calculator = new UsageCalculator(programs, Environment.GetFolderPath(Environment.SpecialFolder.Windows), shared);

            foreach (var entry in inventory)
            {
                _items[entry.Key] = new AppItem(entry.Key, entry.Program.Name, entry.Program, entry.SharesFolderWith, entry.MeasureFolder);
            }

            RefreshSizes();
            IsLoaded = true;

            // Measuring every folder can take a minute: the page is usable meanwhile and fills in as sizes arrive.
            _ = MeasureAsync(inventory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not load the installed applications");
            IsLoaded = true;
        }
    }

    private async Task MeasureAsync(IReadOnlyList<InventoryEntry> inventory)
    {
        var toMeasure = inventory
            .Where(entry => entry.MeasureFolder is not null && Directory.Exists(entry.MeasureFolder))
            .OrderByDescending(entry => entry.Program.EstimatedSizeBytes ?? 0)
            .ToList();
        _toMeasureCount = toMeasure.Count;
        _measuredCount = 0;
        OnMeasureProgressChanged();

        using var gate = new SemaphoreSlim(ParallelMeasures);
        await Task.WhenAll(toMeasure.Select(async entry =>
        {
            await gate.WaitAsync();
            try
            {
                var size = await _folderSizer.MeasureAsync(entry.MeasureFolder!, entry.ExcludedFolders, CancellationToken.None);
                _items[entry.Key].SetMeasuredSize(size);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not measure {Folder}", entry.MeasureFolder);
            }
            finally
            {
                gate.Release();
                _measuredCount++;
                _sizesChanged = true;
                OnMeasureProgressChanged();
            }
        }));

        RefreshSizes();
    }

    private async Task SampleAsync()
    {
        if (_isSampling || _calculator is null)
        {
            return;
        }

        _isSampling = true;
        try
        {
            var now = DateTime.UtcNow;
            var samples = await Task.Run(_monitor.Sample);
            var usages = _previousSamples.Count == 0
                ? []
                : _calculator.Compute(_previousSamples, samples, now - _previousSampleTime, _monitor.ProcessorCount);
            _previousSamples = samples;
            _previousSampleTime = now;

            if (usages.Count > 0)
            {
                ApplyUsages(usages);
            }

            if (_sizesChanged)
            {
                RefreshSizes();
            }
        }
        finally
        {
            _isSampling = false;
        }
    }

    private void ApplyUsages(IReadOnlyList<RunningAppUsage> usages)
    {
        var byKey = usages.ToDictionary(usage => usage.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var usage in usages.Where(usage => !_items.ContainsKey(usage.Key)))
        {
            _items[usage.Key] = new AppItem(usage.Key, usage.DisplayName, null, null, null);
        }

        foreach (var item in _items.Values.ToList())
        {
            if (byKey.TryGetValue(item.Key, out var usage))
            {
                item.Update(usage);
            }
            else if (item.IsRunning || item.CpuHistory.Count > 0)
            {
                item.Update(null);
            }

            // Programs that are not installed only exist here while they run.
            if (!item.IsInstalled && !item.IsRunning && item != SelectedApp)
            {
                _items.Remove(item.Key);
            }
        }

        _totalCpu = Math.Min(100, usages.Sum(usage => usage.CpuPercent));
        _totalMemory = usages.Sum(usage => usage.MemoryBytes);
        _runningCount = usages.Count(usage => usage.Key is not UsageCalculator.WindowsKey and not UsageCalculator.ProtectedKey);
        Push(_cpuHistory, _totalCpu);
        Push(_memoryHistory, _totalMemory);

        SyncRunning();
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(MemoryRatio));
        OnPropertyChanged(nameof(RunningCountText));
        OnPropertyChanged(nameof(CpuHistory));
        OnPropertyChanged(nameof(MemoryHistory));
    }

    // Moves rows in place instead of rebuilding the list, so a row being looked at does not flicker.
    private void SyncRunning()
    {
        var running = _items.Values.Where(item => item.IsRunning).ToList();
        var ordered = (SortRunningByMemory
                ? running.OrderByDescending(item => item.MemoryBytes)
                : running.OrderByDescending(item => item.CpuPercent).ThenByDescending(item => item.MemoryBytes))
            .Take(RunningRowCount)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var current = RunningApps.IndexOf(ordered[index]);
            if (current == index)
            {
                continue;
            }

            if (current >= 0)
            {
                RunningApps.Move(current, index);
            }
            else
            {
                RunningApps.Insert(index, ordered[index]);
            }
        }

        while (RunningApps.Count > ordered.Count)
        {
            RunningApps.RemoveAt(RunningApps.Count - 1);
        }

        _otherRunningCount = running.Count - ordered.Count;
        OnPropertyChanged(nameof(OtherRunningText));
    }

    private void RefreshSizes()
    {
        _sizesChanged = false;
        var installed = Installed.ToList();
        var total = installed.Sum(item => item.SizeBytes);
        var largest = installed.Select(item => item.SizeBytes).DefaultIfEmpty(0).Max();
        foreach (var item in installed)
        {
            item.SetShare(total, largest);
        }

        var bySize = installed.Where(item => item.SizeBytes > 0).OrderByDescending(item => item.SizeBytes).ToList();
        var tiles = bySize
            .Take(TreemapTileCount)
            .Select(item => new TreemapTile(item.Key, item.Name, item.GroupLabel, item.SizeText, item.SizeBytes, ChartPalette.Of(item.Group)))
            .ToList();
        var rest = bySize.Skip(TreemapTileCount).ToList();
        if (rest.Count > 0)
        {
            // A long tail of tiny tiles would be unreadable: it becomes one gray tile.
            var restBytes = rest.Sum(item => item.SizeBytes);
            tiles.Add(new TreemapTile(OthersKey, $"{rest.Count} autres applications", "petites applications regroupées", ByteSize.Format(restBytes), restBytes, ChartPalette.Other));
        }

        Tiles = tiles;

        var groups = installed
            .GroupBy(item => item.Group)
            .Select(group => (Group: group.Key, Size: group.Sum(item => item.SizeBytes), Count: group.Count()))
            .OrderByDescending(group => group.Size)
            .ToList();
        var largestGroup = groups.Select(group => group.Size).DefaultIfEmpty(0).Max();
        Groups = groups.Select(group => new GroupShareRow(group.Group, group.Size, group.Count, total, largestGroup)).ToList();

        OnPropertyChanged(nameof(InstalledCountText));
        OnPropertyChanged(nameof(TotalSizeText));
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        var matches = Installed.Where(item => query.Length == 0
            || item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.GroupLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        FilteredApps = (SortIndex switch
        {
            1 => matches.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => matches.OrderByDescending(item => item.Program?.InstalledOn ?? DateOnly.MinValue),
            3 => matches.OrderByDescending(item => item.IsRunning).ThenByDescending(item => item.SizeBytes),
            _ => matches.OrderByDescending(item => item.SizeBytes),
        }).ToList();
    }

    private void OnMeasureProgressChanged()
    {
        OnPropertyChanged(nameof(TotalSizeCaption));
        OnPropertyChanged(nameof(MeasureProgress));
        OnPropertyChanged(nameof(IsMeasuring));
    }

    private static void Push(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > LiveCapacity)
        {
            history.Dequeue();
        }
    }

    private static IReadOnlyList<string> SharedFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            profile,
            local,
            Path.Combine(local, "Programs"),
            Path.Combine(profile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        }.Where(path => path.Length > 0).ToList();
    }
}
