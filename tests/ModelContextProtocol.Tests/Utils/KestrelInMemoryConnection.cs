using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using System.IO.Pipelines;

namespace ModelContextProtocol.Tests.Utils;

public sealed class KestrelInMemoryConnection : ConnectionContext, IDisposable
{
    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly CancellationTokenSource _connectionClosedTokenSource = new CancellationTokenSource();
    private readonly IFeatureCollection _features = new FeatureCollection();

    private int _isClosed;

    public KestrelInMemoryConnection()
    {
        ConnectionClosed = _connectionClosedTokenSource.Token;
        Transport = new DuplexPipe
        {
            Input = _clientToServerPipe.Reader,
            Output = _serverToClientPipe.Writer,
        };
        Application = new DuplexPipe
        {
            Input = _serverToClientPipe.Reader,
            Output = _clientToServerPipe.Writer,
        };
        ClientStream = new DuplexStream(Application);
    }

    public IDuplexPipe Application { get; }
    public Stream ClientStream { get;  }

    public override IDuplexPipe Transport { get; set; }
    public override string ConnectionId { get; set; } = Guid.NewGuid().ToString("N");

    public override IFeatureCollection Features => _features;

    public override IDictionary<object, object?> Items { get; set; } = new Dictionary<object, object?>();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isClosed, 1) == 1)
        {
            return;
        }

        _clientToServerPipe.Writer.Complete();
        _serverToClientPipe.Writer.Complete();
        _connectionClosedTokenSource.Dispose();
    }

    private class DuplexPipe : IDuplexPipe
    {
        public required PipeReader Input { get; init; }
        public required PipeWriter Output { get; init; }
    }

    private class DuplexStream(IDuplexPipe duplexPipe) : Stream
    {
        private readonly Stream _readStream = duplexPipe.Input.AsStream();
        private readonly Stream _writeStream = duplexPipe.Output.AsStream();

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _readStream.Read(buffer, offset, count);
        public override void Write(byte[] buffer, int offset, int count) => _writeStream.Write(buffer, offset, count);

        public override void Flush() => _writeStream.Flush();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _readStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _readStream.ReadAsync(buffer, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _writeStream.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => _writeStream.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken)
            => _writeStream.FlushAsync(cancellationToken);
    }
}

