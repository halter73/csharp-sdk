using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ModelContextProtocol.Client;

/// <summary>
/// A <see cref="ChannelReader{T}"/> implementation that delegates to another reader
/// after a connection has been established.
/// </summary>
/// <typeparam name="T">The type of data in the channel.</typeparam>
internal sealed class DelegatingChannelReader<T> : ChannelReader<T>
{
    private readonly TaskCompletionSource<bool> _connectionEstablished;
    private readonly AutoDetectingClientSessionTransport _parent;

    public DelegatingChannelReader(AutoDetectingClientSessionTransport parent)
    {
        _parent = parent;
        _connectionEstablished = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// Signals that the transport has been established and operations can proceed.
    /// </summary>
    public void SetConnected()
    {
        _connectionEstablished.TrySetResult(true);
    }

    /// <summary>
    /// Sets the error if connection couldn't be established.
    /// </summary>
    public void SetError(Exception exception)
    {
        _connectionEstablished.TrySetException(exception);
    }

    /// <summary>
    /// Gets the channel reader to delegate to.
    /// </summary>
    private ChannelReader<T> GetReader()
    {
        if (_connectionEstablished.Task.Status != TaskStatus.RanToCompletion)
        {
            throw new InvalidOperationException("Transport connection not yet established.");
        }

        return (_parent.ActiveTransport?.MessageReader as ChannelReader<T>)!;
    }

#if !NETSTANDARD2_0
    /// <inheritdoc/>
    public override bool CanCount => GetReader().CanCount;

    /// <inheritdoc/>
    public override bool CanPeek => GetReader().CanPeek;

    /// <inheritdoc/>
    public override int Count => GetReader().Count;
#endif

    /// <inheritdoc/>
    public override bool TryPeek(out T item)
    {
        try
        {
            return GetReader().TryPeek(out item!);
        }
        catch (InvalidOperationException)
        {
            item = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public override bool TryRead(out T item)
    {
        try
        {
            return GetReader().TryRead(out item!);
        }
        catch (InvalidOperationException)
        {
            item = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        // First wait for the connection to be established
        if (_connectionEstablished.Task.Status != TaskStatus.RanToCompletion)
        {
            return new ValueTask<bool>(WaitForConnectionAndThenReadAsync(cancellationToken));
        }

        // Then delegate to the active reader
        return GetReader().WaitToReadAsync(cancellationToken);
    }

    private async Task<bool> WaitForConnectionAndThenReadAsync(CancellationToken cancellationToken)
    {
        await _connectionEstablished.Task.ConfigureAwait(false);
        return await GetReader().WaitToReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        // First wait for the connection to be established
        if (_connectionEstablished.Task.Status != TaskStatus.RanToCompletion)
        {
            return new ValueTask<T>(WaitForConnectionAndThenGetItemAsync(cancellationToken));
        }

        // Then delegate to the active reader
        return GetReader().ReadAsync(cancellationToken);
    }

    private async Task<T> WaitForConnectionAndThenGetItemAsync(CancellationToken cancellationToken)
    {
        await _connectionEstablished.Task.ConfigureAwait(false);
        return await GetReader().ReadAsync(cancellationToken).ConfigureAwait(false);
    }

#if NETSTANDARD2_0
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        // Create a simple async enumerable implementation
        async IAsyncEnumerable<T> ReadAllAsyncImplementation()
        {
            while (await WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (TryRead(out var item))
                {
                    yield return item;
                }
            }
        }

        return ReadAllAsyncImplementation();
    }
#else
    /// <inheritdoc/>
    public override IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return base.ReadAllAsync(cancellationToken);
    }
#endif
}