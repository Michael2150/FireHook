using FirebirdSql.Data.FirebirdClient;

namespace FireHook.Listening;

public sealed class FbEventListener : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string[] _eventNames;
    private readonly Func<string, Task> _onEvent;
    private readonly TimeSpan _reconnectDelay;

    private CancellationTokenSource? _cts;
    private Task? _reconnectLoop;

    // Signals when QueueEvents has been called and the listener is live.
    private readonly TaskCompletionSource _attached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FbEventListener(
        string connectionString,
        IEnumerable<string> eventNames,
        Func<string, Task> onEvent,
        TimeSpan reconnectDelay = default)
    {
        _connectionString = connectionString;
        _eventNames = eventNames.ToArray();
        _onEvent = onEvent;
        _reconnectDelay = reconnectDelay == default ? TimeSpan.FromSeconds(5) : reconnectDelay;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _reconnectLoop = RunWithReconnectAsync(_cts.Token);
        // Wait until QueueEvents has been called before returning to the caller.
        await _attached.Task.WaitAsync(ct);
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_reconnectLoop is not null)
            await _reconnectLoop.ConfigureAwait(false);
    }

    private async Task RunWithReconnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await AttachAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _attached.TrySetException(ex);
                // swallow and reconnect
            }

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(_reconnectDelay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task AttachAsync(CancellationToken ct)
    {
        var errorTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = ct.Register(() => errorTcs.TrySetCanceled(ct));
        using var re = new FbRemoteEvent(_connectionString);

        re.RemoteEventCounts += async (_, e) =>
        {
            if (e.Counts > 0)
                await _onEvent(e.Name);
        };

        re.RemoteEventError += (_, _) => errorTcs.TrySetResult();

        await re.OpenAsync(ct);
        re.QueueEvents(_eventNames);
        _attached.TrySetResult();

        await errorTcs.Task;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
