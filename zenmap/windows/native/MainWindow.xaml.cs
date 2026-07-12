using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Views;

namespace Zenmap.Windows;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Type> _pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scan"] = typeof(ScanPage),
        ["hosts"] = typeof(PlaceholderPage),
        ["ports"] = typeof(PlaceholderPage),
        ["services"] = typeof(PlaceholderPage),
        ["details"] = typeof(PlaceholderPage),
        ["topology"] = typeof(PlaceholderPage),
        ["saved"] = typeof(PlaceholderPage),
        ["compare"] = typeof(PlaceholderPage),
        ["profiles"] = typeof(PlaceholderPage),
        ["settings"] = typeof(PlaceholderPage),
    };

    public MainWindow()
    {
        InitializeComponent();
        NavigateTo("scan");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag, item.Content?.ToString());
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo("settings", "Settings");
        }
    }

    private void NavigateTo(string tag, string? title = null)
    {
        if (!_pages.TryGetValue(tag, out var pageType))
        {
            return;
        }

        var page = (Page)Activator.CreateInstance(pageType)!;
        if (page is PlaceholderPage placeholder && title is not null)
        {
            placeholder.SetTitle(title);
        }

        ContentFrame.Content = page;
        StatusText.Text = title ?? "Ready";
    }
}
