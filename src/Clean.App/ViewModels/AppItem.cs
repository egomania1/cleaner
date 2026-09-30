using System.Globalization;
using Clean.App.Controls;
using Clean.Core.Apps;
using Clean.Core.Formatting;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

// One application, installed or only seen running. Its live values are refreshed in place, so the
// rows and the detail panel update without being rebuilt.
public sealed class AppItem : ObservableObject
{
    public const int HistoryLength = 40;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _memoryHistory = new();
    private long _sizeBytes;
    private bool _isMeasured;
    private double _shareOfTotal;
    private double _barRatio;
    private bool _isRunning;
    private double _cpuPercent;
    private long _memoryBytes;
    private int _processCount;
    private string? _executablePath;

    public AppItem(string key, string name, ProgramInfo? program, string? sharesFolderWith, string? measureFolder)
    {
        Key = key;
        Name = name;
        Program = program;
        SharesFolderWith = sharesFolderWith;
        MeasureFolder = measureFolder;
        Profile = AppGuide.Find(name) ?? AppGuide.Find(program?.Location is { } location ? Path.GetFileName(location.TrimEnd('\\')) : null);
        Group = key == UsageCalculator.WindowsKey || key == UsageCalculator.ProtectedKey
            ? AppGroup.SystemAndDrivers
            : AppGroups.Of(Profile, program?.Publisher);
        _sizeBytes = sharesFolderWith is null ? program?.EstimatedSizeBytes ?? 0 : 0;
    }

    public string Key { get; }

    public string Name { get; }

    public ProgramInfo? Program { get; }

    public AppProfile? Profile { get; }

    public AppGroup Group { get; }

    public string? MeasureFolder { get; }

    public string? SharesFolderWith { get; }

    public bool IsInstalled => Program is not null;

    public string GroupLabel => AppGroups.Label(Group);

    public SolidColorBrush GroupBrush => ChartPalette.BrushOf(Group);

    public string Publisher => Program?.Publisher is { Length: > 0 } publisher ? publisher : "Éditeur inconnu";

    public string VersionText => Program?.Version is { Length: > 0 } version ? $"Version {version}" : "Version inconnue";

    public string InstalledOnText => Program?.InstalledOn is { } date
        ? $"Installée le {date.ToString("d MMMM yyyy", French)}"
        : IsInstalled ? "Date d'installation inconnue" : "Pas dans la liste des applications installées";

    public string? LocationText => MeasureFolder ?? Program?.Location ?? _executablePath;

    public bool HasLocation => LocationText is not null;

    public string Description => Profile?.WhatItIs
        ?? (IsInstalled
            ? $"Application installée par {Publisher}. Clean ne la connaît pas encore en détail."
            : Key == UsageCalculator.WindowsKey
                ? "Les programmes de Windows lui-même : l'explorateur, les services, la recherche, les mises à jour…"
                : Key == UsageCalculator.ProtectedKey
                    ? "Processus que Windows protège : Clean peut voir leur mémoire, mais pas quel programme ils sont."
                    : "Programme en cours d'exécution qui n'est pas enregistré comme application installée (portable ou lancé depuis un dossier).");

    public string WhereSpaceGoes => Profile?.WhereSpaceGoes ?? SizeOrigin;

    public long SizeBytes
    {
        get => _sizeBytes;
        private set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
            }
        }
    }

    public string SizeText => IsInstalled ? ByteSize.Format(SizeBytes) : "—";

    public string SizeOrigin => SharesFolderWith is not null
        ? $"Partage son dossier avec {SharesFolderWith} : sa place est comptée avec lui."
        : _isMeasured
            ? "Taille mesurée sur le disque."
            : MeasureFolder is not null
                ? "Mesure en cours, taille annoncée par l'installateur en attendant."
                : "Taille annoncée par l'installateur à Windows (pas de dossier propre à mesurer).";

    public double ShareOfTotal
    {
        get => _shareOfTotal;
        private set
        {
            if (SetProperty(ref _shareOfTotal, value))
            {
                OnPropertyChanged(nameof(ShareText));
            }
        }
    }

    public string ShareText => IsInstalled
        ? $"{(ShareOfTotal * 100).ToString(ShareOfTotal < 0.01 ? "0.0" : "0", French)} % de la place prise par les applications"
        : "Hors applications installées";

    public double BarRatio
    {
        get => _barRatio;
        private set
        {
            if (SetProperty(ref _barRatio, value))
            {
                OnPropertyChanged(nameof(BarWidth));
                OnPropertyChanged(nameof(BarRest));
            }
        }
    }

    public GridLength BarWidth => new(Math.Max(BarRatio, 0.002), GridUnitType.Star);

    public GridLength BarRest => new(Math.Max(1 - BarRatio, 0.0001), GridUnitType.Star);

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(RunningText));
                OnPropertyChanged(nameof(RunningVisibility));
            }
        }
    }

    public Visibility RunningVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    public string RunningText => IsRunning
        ? $"En cours — {ProcessCount} processus"
        : "Pas lancée en ce moment";

    public double CpuPercent => _cpuPercent;

    public long MemoryBytes => _memoryBytes;

    public int ProcessCount => _processCount;

    public string CpuText => IsRunning ? $"{_cpuPercent.ToString(_cpuPercent < 10 ? "0.0" : "0", French)} %" : "—";

    public string MemoryText => IsRunning ? ByteSize.Format(_memoryBytes) : "—";

    public double CpuBar => Math.Clamp(_cpuPercent / 100, 0, 1);

    public IReadOnlyList<double> CpuHistory => _cpuHistory.ToList();

    public IReadOnlyList<double> MemoryHistory => _memoryHistory.ToList();

    // At least 5 %: below that, noise around zero would look like big swings.
    public double CpuHistoryMaximum => Math.Max(5, _cpuHistory.DefaultIfEmpty(0).Max() * 1.2);

    public double MemoryHistoryMaximum => Math.Max(1, _memoryHistory.DefaultIfEmpty(0).Max() * 1.2);

    public void SetMeasuredSize(long bytes)
    {
        _isMeasured = true;
        SizeBytes = bytes;
        OnPropertyChanged(nameof(SizeOrigin));
        OnPropertyChanged(nameof(WhereSpaceGoes));
    }

    public void SetShare(long totalBytes, long largestBytes)
    {
        ShareOfTotal = totalBytes > 0 ? (double)SizeBytes / totalBytes : 0;
        BarRatio = largestBytes > 0 ? (double)SizeBytes / largestBytes : 0;
    }

    public void Update(RunningAppUsage? usage)
    {
        _cpuPercent = usage?.CpuPercent ?? 0;
        _memoryBytes = usage?.MemoryBytes ?? 0;
        _processCount = usage?.ProcessCount ?? 0;
        _executablePath ??= usage?.ExecutablePath;
        Push(_cpuHistory, _cpuPercent);
        Push(_memoryHistory, _memoryBytes);
        IsRunning = usage is not null;

        OnPropertyChanged(nameof(CpuPercent));
        OnPropertyChanged(nameof(MemoryBytes));
        OnPropertyChanged(nameof(ProcessCount));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(CpuBar));
        OnPropertyChanged(nameof(RunningText));
        OnPropertyChanged(nameof(CpuHistory));
        OnPropertyChanged(nameof(MemoryHistory));
        OnPropertyChanged(nameof(MemoryHistoryMaximum));
        OnPropertyChanged(nameof(CpuHistoryMaximum));
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(HasLocation));
    }

    private static void Push(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > HistoryLength)
        {
            history.Dequeue();
        }
    }
}
