using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class ProfilesPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly MainWindow _window;

    public ProfilesPage(ZenmapAppState state, MainWindow window)
    {
        InitializeComponent();
        _state = state;
        _window = window;
        Refresh();
    }

    public void Refresh()
    {
        ProfilesList.ItemsSource = ResultsFiltering.FilterProfiles(_state.Profiles, FilterBox.Text).ToList();
    }

    private ScanProfile? SelectedProfile() => ProfilesList.SelectedItem as ScanProfile;

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void UseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedProfile() is { } profile)
        {
            _state.UseProfile(profile);
        }
    }

    private async void AddButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (await ShowEditorAsync(null) is { } values)
        {
            _state.AddCustomProfile(values.Name, values.Arguments, values.Description);
            Refresh();
        }
    }

    private async void EditButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedProfile() is { IsBuiltIn: false } profile &&
            await ShowEditorAsync(profile) is { } values)
        {
            _state.UpdateCustomProfile(profile.Id, values.Name, values.Arguments, values.Description);
            Refresh();
        }
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedProfile() is { IsBuiltIn: false } profile)
        {
            _state.DeleteCustomProfile(profile.Id);
            Refresh();
        }
    }

    private void DuplicateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedProfile() is { } profile)
        {
            _state.DuplicateProfile(profile);
            Refresh();
        }
    }

    private async void ImportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await FileDialogService.PickOpenJsonAsync(_window);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.ImportProfiles(path);
            Refresh();
        }
    }

    private async void ExportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await FileDialogService.PickSaveTextAsync(_window, "zenmap-profiles.json");
        if (!string.IsNullOrWhiteSpace(path))
        {
            _state.ExportProfiles(path);
        }
    }

    private async Task<(string Name, string Arguments, string Description)?> ShowEditorAsync(ScanProfile? profile)
    {
        var nameBox = new TextBox { Header = "Name", Text = profile?.Name ?? "" };
        var argsBox = new TextBox { Header = "Arguments", Text = profile?.Arguments ?? "", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
        var descriptionBox = new TextBox { Header = "Description", Text = profile?.Description ?? "", AcceptsReturn = true };
        var panel = new StackPanel { Spacing = 8, Width = 420 };
        panel.Children.Add(nameBox);
        panel.Children.Add(argsBox);
        panel.Children.Add(descriptionBox);

        var dialog = new ContentDialog
        {
            Title = profile is null ? "Add profile" : "Edit profile",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return null;
        }

        return (nameBox.Text.Trim(), argsBox.Text.Trim(), descriptionBox.Text.Trim());
    }
}
