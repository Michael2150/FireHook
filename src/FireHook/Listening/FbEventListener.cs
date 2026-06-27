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

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _reconnectLoop = RunWithReconnectAsync(_cts.Token);
        return Task.CompletedTask;
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
            catch
            {
                // swallow connection errors and reconnect
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

        re.QueueEvents(_eventNames);

        await errorTcs.Task;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
