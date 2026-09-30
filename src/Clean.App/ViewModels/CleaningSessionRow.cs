using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.Input;

namespace Clean.App.ViewModels;

public sealed class CleaningSessionRow(
    CleaningSession session,
    DateTimeOffset now,
    Func<CleaningSession, Task> restore,
    Func<CleaningSession, Task> free)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public CleaningSession Session => session;

    public IAsyncRelayCommand RestoreCommand { get; } = new AsyncRelayCommand(() => restore(session), () => session.CanRestore);

    public IAsyncRelayCommand FreeCommand { get; } = new AsyncRelayCommand(() => free(session), () => session.CanRestore);

    public bool CanRestore => session.CanRestore;

    public string DateText => $"{RelativeDate.Format(session.CleanedAt.ToLocalTime(), now.ToLocalTime())} à {session.CleanedAt.ToLocalTime().ToString("HH:mm", French)}";

    public string SizeText => ByteSize.Format(session.SizeBytes);

    public string FilesText => $"{session.FileCount.ToString("N0", French)} fichiers sur {session.DriveName}";

    public string LocationsText => string.Join(", ", session.Locations.Select(location => location.Name).Distinct());

    public string StatusText => session.Status switch
    {
        CleaningSessionStatus.Restorable => $"RESTAURABLE JUSQU'AU {session.ExpiresAt.ToLocalTime().ToString("d MMMM", French).ToUpper(French)}",
        CleaningSessionStatus.Restored => "RESTAURÉ",
        _ => "ESPACE LIBÉRÉ",
    };

    public string? ClosedText => session.ClosedAt is { } closed
        ? $"{(session.Status == CleaningSessionStatus.Restored ? "Restauré" : "Libéré")} le {closed.ToLocalTime().ToString("d MMMM yyyy à HH:mm", French)}"
        : null;

    public bool HasClosedText => ClosedText is not null;
}
