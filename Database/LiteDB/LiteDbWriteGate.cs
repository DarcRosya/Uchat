using System;
using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.LiteDB;

public sealed class LiteDbWriteGate : ILiteDbWriteGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new GateToken(_semaphore);
    }

    private sealed class GateToken : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public GateToken(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _semaphore.Release();
            _disposed = true;
        }
    }
}
