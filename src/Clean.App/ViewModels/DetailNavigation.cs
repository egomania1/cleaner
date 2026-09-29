using Clean.Core.Models;

namespace Clean.App.ViewModels;

public sealed record DetailNavigation(
    Action Close,
    Action<StorageUsage> OpenChild,
    Action? GoBack,
    string? BackLabel);
