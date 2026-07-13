using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Zenmap.Windows.Models;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class TopologyPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private readonly Action<ScannedHost> _onShowDetails;
    private int? _selectedIndex;

    public TopologyPage(ZenmapAppState state, Action<ScannedHost> onShowDetails)
    {
        InitializeComponent();
        _state = state;
        _onShowDetails = onShowDetails;
        TopologyCanvas.SizeChanged += (_, _) => DrawTopology();
        Refresh();
    }

    public void Refresh()
    {
        CountText.Text = $"{_state.Hosts.Count} host{(_state.Hosts.Count == 1 ? "" : "s")}";
        EmptyText.Visibility = _state.Hosts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TopologyCanvas.Visibility = _state.Hosts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        DrawTopology();
    }

    private void DrawTopology()
    {
        TopologyCanvas.Children.Clear();
        var width = TopologyCanvas.ActualWidth;
        var height = TopologyCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var center = new Point(width / 2, height / 2);
        var centerNode = new Ellipse { Width = 48, Height = 48, Fill = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 90, 140, 240)) };
        Canvas.SetLeft(centerNode, center.X - 24);
        Canvas.SetTop(centerNode, center.Y - 24);
        TopologyCanvas.Children.Add(centerNode);

        for (var index = 0; index < _state.Hosts.Count; index++)
        {
            var point = PointForHost(index, width, height);
            var line = new Line
            {
                X1 = center.X,
                Y1 = center.Y,
                X2 = point.X,
                Y2 = point.Y,
                Stroke = new SolidColorBrush(global::Windows.UI.Color.FromArgb(120, 180, 180, 190)),
                StrokeThickness = 1.2,
            };
            TopologyCanvas.Children.Add(line);

            var fill = index == _selectedIndex
                ? global::Windows.UI.Color.FromArgb(255, 240, 190, 50)
                : global::Windows.UI.Color.FromArgb(255, 60, 190, 110);
            var node = new Ellipse { Width = 32, Height = 32, Fill = new SolidColorBrush(fill), Tag = index };
            Canvas.SetLeft(node, point.X - 16);
            Canvas.SetTop(node, point.Y - 16);
            TopologyCanvas.Children.Add(node);

            var label = new TextBlock
            {
                Text = _state.Hosts[index].DisplayName,
                FontSize = 11,
            };
            Canvas.SetLeft(label, point.X - 30);
            Canvas.SetTop(label, point.Y + 18);
            TopologyCanvas.Children.Add(label);
        }
    }

    private Point PointForHost(int index, double width, double height)
    {
        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(width, height) * 0.34;
        if (_state.Hosts.Count == 1)
        {
            return new Point(centerX, centerY - radius * 0.35);
        }

        var angle = (2 * Math.PI * index / _state.Hosts.Count) - (Math.PI / 2);
        return new Point(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));
    }

    private void TopologyCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(TopologyCanvas).Position;
        _selectedIndex = null;
        for (var index = 0; index < _state.Hosts.Count; index++)
        {
            var point = PointForHost(index, TopologyCanvas.ActualWidth, TopologyCanvas.ActualHeight);
            var distance = Math.Sqrt(Math.Pow(point.X - position.X, 2) + Math.Pow(point.Y - position.Y, 2));
            if (distance <= 18)
            {
                _selectedIndex = index;
                break;
            }
        }

        DrawTopology();
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex is int index && index >= 0 && index < _state.Hosts.Count)
        {
            _onShowDetails(_state.Hosts[index]);
        }
    }
}
