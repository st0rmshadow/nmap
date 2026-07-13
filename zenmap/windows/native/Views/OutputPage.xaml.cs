using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Zenmap.Windows.ViewModels;

namespace Zenmap.Windows.Views;

public sealed partial class OutputPage : Page, IZenmapPage
{
    private readonly ZenmapAppState _state;
    private int _findSelection;
    private bool _clampBusy;
    private int _pendingScrollToEnd;

    public OutputPage(ZenmapAppState state)
    {
        InitializeComponent();
        _state = state;
        _state.OutputAppended += OnOutputAppended;

        // Bubble + handledEventsToo: own wheel even if a child marks Handled without moving.
        OutputBorder.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnOutputPointerWheelChanged),
            handledEventsToo: true);

        Loaded += OutputPage_Loaded;
        Refresh();
    }

    private void OutputPage_Loaded(object sender, RoutedEventArgs e) =>
        ClampOutputViewport();

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ClampOutputViewport();

    /// <summary>
    /// Force a finite OutputBorder height from the page's ActualHeight. If a parent measured
    /// us with infinite height, a * row collapses to content size and ScrollViewer grows
    /// with the TextBlock (ScrollableHeight=0 → dead wheel + dead auto-scroll).
    /// </summary>
    private void ClampOutputViewport()
    {
        if (_clampBusy || RootGrid.ActualHeight <= 0)
        {
            return;
        }

        _clampBusy = true;
        try
        {
            var findHeight = FindBar.Visibility == Visibility.Visible ? FindBar.ActualHeight : 0;
            var spacing = 8.0; // RowSpacing between three rows
            var padding = RootGrid.Padding.Top + RootGrid.Padding.Bottom;
            var reserved = HeaderRow.ActualHeight + findHeight + (spacing * 2) + padding;
            var available = Math.Max(80.0, RootGrid.ActualHeight - reserved);
            if (Math.Abs(OutputBorder.Height - available) > 0.5 || double.IsNaN(OutputBorder.Height))
            {
                OutputBorder.Height = available;
                OutputBorder.MaxHeight = available;
            }

            OutputScrollViewer.UpdateLayout();
        }
        finally
        {
            _clampBusy = false;
        }
    }

    private void OutputScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e) =>
        OnOutputPointerWheelChanged(sender, e);

    private void OnOutputPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(OutputScrollViewer).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        if (OutputScrollViewer.ScrollableHeight <= 0)
        {
            ClampOutputViewport();
            if (OutputScrollViewer.ScrollableHeight <= 0)
            {
                return;
            }
        }

        var pos = e.GetCurrentPoint(OutputBorder).Position;
        if (pos.X < 0 || pos.Y < 0 || pos.X > OutputBorder.ActualWidth || pos.Y > OutputBorder.ActualHeight)
        {
            return;
        }

        ApplyWheelDelta(delta);
        e.Handled = true;
    }

    public bool IsPointerOverOutputArea(PointerRoutedEventArgs e)
    {
        try
        {
            if (OutputBorder.ActualWidth <= 0 || OutputBorder.ActualHeight <= 0)
            {
                return false;
            }

            var pt = e.GetCurrentPoint(OutputBorder).Position;
            return pt.X >= 0 && pt.Y >= 0 &&
                   pt.X <= OutputBorder.ActualWidth &&
                   pt.Y <= OutputBorder.ActualHeight;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>No-op: Win32 EDIT overlay removed; XAML TextBlock is always the visible output.</summary>
    public void SetOutputVisible(bool visible) => _ = visible;

    public bool ApplyWheelDelta(
        int mouseWheelDelta,
        out double before,
        out double after,
        out double scrollable)
    {
        before = OutputScrollViewer.VerticalOffset;
        scrollable = OutputScrollViewer.ScrollableHeight;
        if (mouseWheelDelta == 0 || scrollable <= 0)
        {
            after = before;
            return false;
        }

        var step = Math.Max(48.0, OutputScrollViewer.ViewportHeight * 0.15);
        var target = Math.Clamp(
            before - Math.Sign(mouseWheelDelta) * step * (Math.Abs(mouseWheelDelta) / 120.0),
            0,
            scrollable);
        OutputScrollViewer.ChangeView(null, target, null, disableAnimation: true);
        after = OutputScrollViewer.VerticalOffset;
        return Math.Abs(after - before) > 0.5;
    }

    private void ApplyWheelDelta(int delta) =>
        ApplyWheelDelta(delta, out _, out _, out _);

    private void OutputScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        OutputScrollViewer.Focus(FocusState.Pointer);

    public void Refresh()
    {
        OutputTextBlock.Text = _state.OutputText;
        ClampOutputViewport();
        if (AutoScrollSwitch.IsOn && FindBar.Visibility != Visibility.Visible)
        {
            QueueScrollOutputToEnd();
        }

        UpdateFindSummary();
    }

    private void QueueScrollOutputToEnd()
    {
        // TextBlock height often settles on the next dispatcher tick after Text=…
        var ticket = ++_pendingScrollToEnd;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ticket != _pendingScrollToEnd)
            {
                return;
            }

            ScrollOutputToEndImmediate();
        });
    }

    private void ScrollOutputToEndImmediate()
    {
        ClampOutputViewport();
        OutputScrollViewer.UpdateLayout();
        var target = OutputScrollViewer.ScrollableHeight;
        // ExtentHeight also works (ChangeView clamps); prefer ScrollableHeight.
        if (target <= 0 && OutputScrollViewer.ExtentHeight > OutputScrollViewer.ViewportHeight)
        {
            target = OutputScrollViewer.ExtentHeight - OutputScrollViewer.ViewportHeight;
        }

        OutputScrollViewer.ChangeView(
            horizontalOffset: null,
            verticalOffset: target,
            zoomFactor: null,
            disableAnimation: true);
    }

    public void ShowFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        ClampOutputViewport();
        FindBox.Focus(FocusState.Programmatic);
        UpdateFindSummary();
        ApplyFindSelection();
    }

    public void CopyOutputToClipboard()
    {
        var package = new DataPackage();
        package.SetText(_state.OutputText);
        Clipboard.SetContent(package);
    }

    private void OnOutputAppended(string text)
    {
        OutputTextBlock.Text = _state.OutputText;
        ClampOutputViewport();
        if (AutoScrollSwitch.IsOn && FindBar.Visibility != Visibility.Visible)
        {
            QueueScrollOutputToEnd();
        }

        UpdateFindSummary();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyOutputToClipboard();

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_state.IsScanRunning)
        {
            return;
        }

        _state.ClearOutput();
        Refresh();
    }

    private void FindButton_Click(object sender, RoutedEventArgs e) => ShowFindBar();

    private void FindClose_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        FindBox.Text = "";
        _findSelection = 0;
        FindSummary.Text = "";
        ClampOutputViewport();
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _findSelection = 0;
        UpdateFindSummary();
        ApplyFindSelection();
    }

    private void FindBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            FindNext_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            FindClose_Click(sender, e);
            e.Handled = true;
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var matches = FindMatchIndexes();
        if (matches.Count == 0)
        {
            return;
        }

        _findSelection = (_findSelection + 1) % matches.Count;
        UpdateFindSummary();
        ApplyFindSelection();
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        var matches = FindMatchIndexes();
        if (matches.Count == 0)
        {
            return;
        }

        _findSelection = (_findSelection - 1 + matches.Count) % matches.Count;
        UpdateFindSummary();
        ApplyFindSelection();
    }

    private void UpdateFindSummary()
    {
        var query = FindBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query) || FindBar.Visibility != Visibility.Visible)
        {
            FindSummary.Text = "";
            return;
        }

        var matches = FindMatchIndexes();
        if (matches.Count == 0)
        {
            FindSummary.Text = "No matches";
            return;
        }

        var displayIndex = Math.Min(_findSelection + 1, matches.Count);
        FindSummary.Text = $"{displayIndex} of {matches.Count}";
    }

    private void ApplyFindSelection()
    {
        var query = FindBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var matches = FindMatchIndexes();
        if (matches.Count == 0)
        {
            return;
        }

        _findSelection = Math.Clamp(_findSelection, 0, matches.Count - 1);
        ScrollOuterViewerToCharacterIndex(matches[_findSelection]);
    }

    private void ScrollOuterViewerToCharacterIndex(int characterIndex)
    {
        var text = OutputTextBlock.Text ?? "";
        if (text.Length == 0 || OutputScrollViewer.ExtentHeight <= 0)
        {
            return;
        }

        var newlines = 0;
        var limit = Math.Min(characterIndex, text.Length);
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
            {
                newlines++;
            }
        }

        var totalNewlines = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                totalNewlines++;
            }
        }

        var lineCount = Math.Max(1, totalNewlines + 1);
        var target = OutputScrollViewer.ExtentHeight * (newlines / (double)lineCount);
        var maxOffset = Math.Max(0, OutputScrollViewer.ScrollableHeight);
        OutputScrollViewer.ChangeView(
            horizontalOffset: null,
            verticalOffset: Math.Clamp(target, 0, maxOffset),
            zoomFactor: null,
            disableAnimation: true);
    }

    private List<int> FindMatchIndexes()
    {
        var query = FindBox.Text?.Trim() ?? "";
        var text = OutputTextBlock.Text ?? "";
        var matches = new List<int>();
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
        {
            return matches;
        }

        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            matches.Add(index);
            start = index + Math.Max(1, query.Length);
        }

        return matches;
    }
}
