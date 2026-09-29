using Clean.App.ViewModels;
using Clean.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class StoragePage : Page
{
    public StoragePage()
    {
        InitializeComponent();
    }

    public StorageViewModel ViewModel { get; } = App.Services.GetRequiredService<StorageViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureDisksLoadedAsync();
    }

    private void OnChartBucketSelected(object? sender, StorageUsage bucket) => ShowDetail(bucket);

    private void OnBucketRowClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StorageUsage bucket })
        {
            ShowDetail(bucket);
        }
    }

    private void ShowDetail(StorageUsage bucket)
    {
        ViewModel.SelectBucket(bucket);

        // The card sits below the chart, often outside the visible area; scroll to it once it has been laid out.
        DispatcherQueue.TryEnqueue(() => DetailCard.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.1,
        }));
    }
}
