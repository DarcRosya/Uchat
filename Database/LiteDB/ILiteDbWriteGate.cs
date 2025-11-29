using System;
using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.LiteDB;

/// <summary>
/// Serializes writes to LiteDB so maintenance tasks can pause them.
/// </summary>
public interface ILiteDbWriteGate
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
