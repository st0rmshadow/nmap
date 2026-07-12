using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.Services;

namespace Zenmap.Windows.Views;

public sealed partial class ScanPage : Page
{
    public ScanPage()
    {
        InitializeComponent();
        ProfileCombo.ItemsSource = BuiltInProfiles.All;
        ProfileCombo.DisplayMemberPath = nameof(Models.ScanProfile.Name);
        ProfileCombo.SelectedIndex = 2;
        UpdateCommandPreview();
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ProfileCombo.SelectedItem is Models.ScanProfile profile)
        {
            ArgumentsBox.Text = profile.Arguments;
        }

        UpdateCommandPreview();
    }

    private void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateCommandPreview();
        PrivilegeInfoBar.IsOpen = true;
    }

    private void UpdateCommandPreview()
    {
        var arguments = ArgumentsBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requirement = ScanPrivilegeEvaluator.Evaluate(arguments);
        PrivilegeInfoBar.IsOpen = requirement.Mode == Models.ZenmapScanExecutionMode.Administrator;
        PrivilegeInfoBar.Message = requirement.Reason;

        var command = new Models.ZenmapScanCommand
        {
            BinaryDisplayName = NmapPathResolver.ResolveNmapExecutable(),
            Arguments = arguments,
            Targets = string.IsNullOrWhiteSpace(TargetBox.Text)
                ? Array.Empty<string>()
                : [TargetBox.Text.Trim()],
        };

        CommandPreview.Text = command.DisplayText;
    }
}
