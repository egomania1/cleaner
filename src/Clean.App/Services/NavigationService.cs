namespace Clean.App.Services;

public sealed class NavigationService
{
    public event Action<string>? NavigationRequested;

    public void NavigateTo(string pageTag) => NavigationRequested?.Invoke(pageTag);
}
