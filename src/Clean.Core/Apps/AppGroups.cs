using Clean.Core.Storage;

namespace Clean.Core.Apps;

public static class AppGroups
{
    public static IReadOnlyList<AppGroup> All { get; } = Enum.GetValues<AppGroup>();

    public static string Label(AppGroup group) => group switch
    {
        AppGroup.Games => "Jeux",
        AppGroup.Development => "Développement",
        AppGroup.CreativeAndMedia => "Création et multimédia",
        AppGroup.WebAndChat => "Web et messagerie",
        AppGroup.OfficeAndCloud => "Bureautique et cloud",
        AppGroup.SystemAndDrivers => "Système et pilotes",
        _ => "Autres",
    };

    public static AppGroup Of(AppProfile? profile, string? publisher = null)
    {
        if (profile is not null)
        {
            return profile.Category switch
            {
                "Jeu" or "Lanceur de jeux" => AppGroup.Games,
                "Outil de développement" => AppGroup.Development,
                "Multimédia" or "Création" => AppGroup.CreativeAndMedia,
                "Navigateur web" or "Messagerie" => AppGroup.WebAndChat,
                "Bureautique" or "Stockage en ligne" => AppGroup.OfficeAndCloud,
                "Pilotes et matériel" or "Utilitaire" => AppGroup.SystemAndDrivers,
                _ => AppGroup.Other,
            };
        }

        // Unknown apps from hardware vendors are almost always drivers or their control panels.
        return publisher is not null && HardwareVendors.Any(vendor => publisher.Contains(vendor, StringComparison.OrdinalIgnoreCase))
            ? AppGroup.SystemAndDrivers
            : AppGroup.Other;
    }

    private static readonly string[] HardwareVendors =
        ["NVIDIA", "Advanced Micro Devices", "AMD", "Intel", "Realtek", "Logitech", "Razer", "Corsair", "SteelSeries", "ASUS", "MSI", "Gigabyte"];
}
