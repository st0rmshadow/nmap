using Microsoft.UI.Xaml.Controls;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class OutputPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;

    public OutputPage(ZenmapAppState state)
    {
        InitializeComponent();
        _state = state;
        _state.OutputAppended += OnOutputAppended;
        Refresh();
    }

    public void Refresh()
    {
        OutputBox.Text = _state.OutputText;
        OutputBox.SelectionStart = OutputBox.Text.Length;
    }

    private void OnOutputAppended(string text)
    {
        OutputBox.Text = _state.OutputText;
        OutputBox.SelectionStart = OutputBox.Text.Length;
    }
}
