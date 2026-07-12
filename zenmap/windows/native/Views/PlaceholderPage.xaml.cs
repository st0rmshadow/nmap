using Microsoft.UI.Xaml.Controls;

namespace Zenmap.Windows.Views;

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    public void SetTitle(string title)
    {
        TitleText.Text = title;
    }
}
