using Clean.Core.Interfaces;
using Clean.Core.Licensing;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Licensing;

public sealed class LicenseService : ILicenseService
{
    private readonly LicenseStore _licenses;
    private readonly TrialStore _trials;
    private readonly LicenseVerifier? _verifier;
    private readonly TimeProvider _clock;
    private readonly ILogger<LicenseService> _logger;

    private LicenseCheck? _license;
    private TrialRecord _trial;

    public LicenseService(
        LicenseStore licenses,
        TrialStore trials,
        LicenseVerifier? verifier,
        string deviceId,
        TimeProvider clock,
        ILogger<LicenseService> logger)
    {
        _licenses = licenses;
        _trials = trials;
        _verifier = verifier;
        DeviceId = deviceId;
        _clock = clock;
        _logger = logger;
        _trial = trials.LoadOrStart(clock.GetUtcNow());
        Reload();
    }

    public event Action? Changed;

    public string DeviceId { get; }

    public bool CanVerifyLicenses => _verifier is not null;

#if DEBUG
    // Development builds only: not compiled into the version that is sold. The owner of a sold copy uses a
    // normal signed licence with the "owner" plan, issued by the website.
    public AppAccess Access => Environment.GetEnvironmentVariable("CLEAN_OWNER_ACCESS") == "1"
        ? new AppAccess(AccessMode.Licensed, 0, "Compte propriétaire (développement)", "Accès complet : version de développement.")
        : LicenseEvaluator.Evaluate(_license, _trial, _clock.GetUtcNow());
#else
    public AppAccess Access => LicenseEvaluator.Evaluate(_license, _trial, _clock.GetUtcNow());
#endif

    public void Refresh()
    {
        _trial = _trials.LoadOrStart(_clock.GetUtcNow());
        Reload();
        Changed?.Invoke();
    }

    public LicenseCheck Activate(string token)
    {
        if (_verifier is null)
        {
            return new LicenseCheck(LicenseState.Invalid, null, "La vérification des licences n'est pas encore disponible dans cette version.");
        }

        var check = _verifier.Verify(token, DeviceId, _clock.GetUtcNow());
        if (check.AllowsPaidFeatures)
        {
            _licenses.Save(token);
            _logger.LogInformation("A licence was activated ({State})", check.State);
            Refresh();
        }
        else
        {
            _logger.LogWarning("A licence was refused: {Reason}", check.Reason);
        }

        return check;
    }

    public void Deactivate()
    {
        _licenses.Delete();
        _logger.LogInformation("The licence was removed from this PC");
        Refresh();
    }

    private void Reload() =>
        _license = _verifier is null || _licenses.Load() is not { Length: > 0 } token
            ? null
            : _verifier.Verify(token, DeviceId, _clock.GetUtcNow());
}
