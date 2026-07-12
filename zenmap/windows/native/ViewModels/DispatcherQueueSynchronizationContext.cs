using Microsoft.UI.Dispatching;

namespace Zenmap.Windows.ViewModels;

internal sealed class DispatcherQueueSynchronizationContext : SynchronizationContext
{
    private readonly DispatcherQueue _dispatcher;

    public DispatcherQueueSynchronizationContext(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override void Post(SendOrPostCallback callback, object? state) =>
        _dispatcher.TryEnqueue(() => callback(state));

    public override void Send(SendOrPostCallback callback, object? state) =>
        _dispatcher.TryEnqueue(() => callback(state));
}
