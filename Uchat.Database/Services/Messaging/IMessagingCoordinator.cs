using System.Threading;
using System.Threading.Tasks;

namespace Uchat.Database.Services.Messaging;

/// <summary>
/// Coordinates operations that span SQLite (metadata) and LiteDB (messages).
/// </summary>
public interface IMessagingCoordinator
{
    Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default);
}
