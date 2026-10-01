using Clean.Core.Licensing;

namespace Clean.Core.Interfaces;

public interface ILicenseService
{
    event Action? Changed;

    // Worked out from what was read at the last Refresh and the current time, so it is cheap to ask often.
    AppAccess Access { get; }

    string DeviceId { get; }

    bool CanVerifyLicenses { get; }

    void Refresh();

    // The token is stored only when it is valid for this PC.
    LicenseCheck Activate(string token);

    void Deactivate();
}
