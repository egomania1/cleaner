using Clean.Core.Models;

namespace Clean.Core.Rules;

// First set of cleaning rules, kept in code until the JSON rule engine takes over.
// Paths use environment variables so they resolve to the right folders on any PC.
public static class BuiltInRules
{
    private const string RegeneratedShaderCache =
        "Cache de shaders recréé automatiquement par le pilote graphique. Les jeux peuvent ramer quelques secondes aux premiers lancements, le temps de le reconstruire.";

    public static IReadOnlyList<CleaningRule> All { get; } =
    [
        new("USER_TEMP",
            "Fichiers temporaires de ton compte",
            "Fichiers laissés par les applications et les installations. Ceux modifiés depuis moins de 2 jours sont ignorés, car ils peuvent encore servir.",
            CleaningCategory.Temporary, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\Temp", "%TEMP%", "%TMP%"], MinimumAgeDays: 2, AutomaticCleaningAllowed: true),
        new("WINDOWS_TEMP",
            "Fichiers temporaires de Windows",
            "Fichiers laissés par Windows et les installations. Ceux modifiés depuis moins de 2 jours sont ignorés, car ils peuvent encore servir.",
            CleaningCategory.Temporary, RiskLevel.Safe,
            [@"%WINDIR%\Temp"], MinimumAgeDays: 2, AutomaticCleaningAllowed: true),
        new("WINDOWS_ERROR_REPORTS",
            "Rapports d'erreurs Windows",
            "Rapports envoyés ou en attente d'envoi à Microsoft après un plantage. Inutiles une fois envoyés.",
            CleaningCategory.CrashDumps, RiskLevel.Safe,
            [@"%PROGRAMDATA%\Microsoft\Windows\WER", @"%LOCALAPPDATA%\Microsoft\Windows\WER"], MinimumAgeDays: 7, AutomaticCleaningAllowed: true),
        new("APP_CRASH_DUMPS",
            "Rapports de plantage des applications",
            "Copies de la mémoire enregistrées quand une application a planté. Utiles seulement pour un diagnostic.",
            CleaningCategory.CrashDumps, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\CrashDumps"], MinimumAgeDays: 7, AutomaticCleaningAllowed: true),
        new("NVIDIA_DX_CACHE",
            "Cache des shaders NVIDIA (DirectX)",
            RegeneratedShaderCache,
            CleaningCategory.GpuCache, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\NVIDIA\DXCache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
        new("NVIDIA_GL_CACHE",
            "Cache des shaders NVIDIA (OpenGL)",
            RegeneratedShaderCache,
            CleaningCategory.GpuCache, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\NVIDIA\GLCache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
        new("AMD_DX_CACHE",
            "Cache des shaders AMD (DirectX)",
            RegeneratedShaderCache,
            CleaningCategory.GpuCache, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\AMD\DxCache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
        new("AMD_GL_CACHE",
            "Cache des shaders AMD (OpenGL)",
            RegeneratedShaderCache,
            CleaningCategory.GpuCache, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\AMD\GLCache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
        new("DIRECTX_SHADER_CACHE",
            "Cache des shaders DirectX",
            "Cache de shaders géré par Windows pour tous les jeux et applications 3D. Il se reconstruit automatiquement.",
            CleaningCategory.GpuCache, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\D3DSCache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
        new("NPM_CACHE",
            "Cache npm",
            "Paquets JavaScript déjà téléchargés par npm. Ils se retéléchargent au besoin, mais les prochaines installations seront plus lentes.",
            CleaningCategory.Developer, RiskLevel.Caution,
            [@"%LOCALAPPDATA%\npm-cache", @"%APPDATA%\npm-cache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: false),
        new("PIP_CACHE",
            "Cache pip",
            "Paquets Python déjà téléchargés par pip. Ils se retéléchargent au besoin.",
            CleaningCategory.Developer, RiskLevel.Caution,
            [@"%LOCALAPPDATA%\pip\cache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: false),
        new("NUGET_HTTP_CACHE",
            "Cache de téléchargement NuGet",
            "Copies temporaires des paquets .NET téléchargés. Les paquets installés dans tes projets ne sont pas concernés.",
            CleaningCategory.Developer, RiskLevel.Safe,
            [@"%LOCALAPPDATA%\NuGet\v3-cache"], MinimumAgeDays: 0, AutomaticCleaningAllowed: true),
    ];
}
