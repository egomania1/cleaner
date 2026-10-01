using System.Windows.Input;
using Clean.App.Services;
using Clean.Core.Interfaces;
using Clean.Core.Licensing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.DataTransfer;

namespace Clean.App.ViewModels;

public sealed class LicenseViewModel : ObservableObject
{
    // Set once the website exists; the buy button stays hidden while it is empty.
    public const string PurchaseUrl = "";

    private readonly ILicenseService _license;
    private string _token = string.Empty;
    private string? _activationMessage;

    public LicenseViewModel(ILicenseService license, NavigationService navigation)
    {
        _license = license;
        _license.Changed += OnLicenseChanged;
        ActivateCommand = new RelayCommand(Activate, () => !string.IsNullOrWhiteSpace(Token));
        DeactivateCommand = new RelayCommand(_license.Deactivate, () => IsLicensed);
        CopyDeviceIdCommand = new RelayCommand(CopyDeviceId);
        OpenPurchaseCommand = new AsyncRelayCommand(async () => await Windows.System.Launcher.LaunchUriAsync(new Uri(PurchaseUrl)));
        ShowLicenseCommand = new RelayCommand(() => navigation.NavigateTo("settings"));
    }

    public ICommand ActivateCommand { get; }

    public ICommand DeactivateCommand { get; }

    public ICommand CopyDeviceIdCommand { get; }

    public ICommand OpenPurchaseCommand { get; }

    public ICommand ShowLicenseCommand { get; }

    public string Title => _license.Access.Title;

    public string Detail => _license.Access.Detail;

    public bool IsLicensed => _license.Access.IsLicensed;

    public bool IsNotLicensed => !IsLicensed;

    public bool IsTrialEnded => _license.Access.Mode == AccessMode.TrialEnded;

    // The banner is shown on the pages that remove files, while the trial runs or once it has ended.
    public bool ShowBanner => !IsLicensed;

    public string BannerText => IsTrialEnded
        ? "Essai terminé : l'analyse reste gratuite, mais retirer des fichiers demande la licence."
        : $"Essai gratuit : {_license.Access.DaysLeft} jour{(_license.Access.DaysLeft > 1 ? "s" : string.Empty)} restant{(_license.Access.DaysLeft > 1 ? "s" : string.Empty)}.";

    public string DeviceId => _license.DeviceId;

    public bool CanBuy => PurchaseUrl.Length > 0;

    public bool ShopNotOpen => !CanBuy;

    public bool CanActivate => _license.CanVerifyLicenses;

    public string PriceText => $"{LicenseEvaluator.Price}, achat unique";

    public string Token
    {
        get => _token;
        set
        {
            if (SetProperty(ref _token, value))
            {
                ((RelayCommand)ActivateCommand).NotifyCanExecuteChanged();
            }
        }
    }

    public string? ActivationMessage
    {
        get => _activationMessage;
        private set
        {
            if (SetProperty(ref _activationMessage, value))
            {
                OnPropertyChanged(nameof(HasActivationMessage));
            }
        }
    }

    public bool HasActivationMessage => !string.IsNullOrEmpty(_activationMessage);

    public void Refresh() => _license.Refresh();

    private void Activate()
    {
        var check = _license.Activate(Token);
        ActivationMessage = check.AllowsPaidFeatures ? "Licence activée. Merci !" : check.Reason;
        if (check.AllowsPaidFeatures)
        {
            Token = string.Empty;
        }
    }

    private void CopyDeviceId()
    {
        var package = new DataPackage();
        package.SetText(DeviceId);
        Clipboard.SetContent(package);
        ActivationMessage = "Identifiant copié.";
    }

    private void OnLicenseChanged()
    {
        OnPropertyChanged(string.Empty);
        ((RelayCommand)DeactivateCommand).NotifyCanExecuteChanged();
    }
}
