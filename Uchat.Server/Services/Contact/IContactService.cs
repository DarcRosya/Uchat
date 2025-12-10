using Uchat.Database.Entities;

namespace Uchat.Server.Services.Contact;

public interface IContactService
{
    /// - User1 → User2 (Status = RequestSent)
    /// - User2 → User1 (Status = RequestReceived)
    Task<ServiceResult> SendFriendRequestAsync(int senderId, int receiverId);

    // Status = RequestReceived
    Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetPendingRequestsAsync(int userId);
    Task<ServiceResult<Database.Entities.Contact>> GetContactByIdAsync(int contactId);
    Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetContactsAsync(int userId);

    Task<ServiceResult<int>> AcceptFriendRequestAsync(int userId, int requesterId);
    Task<ServiceResult<int>> RejectFriendRequestAsync(int currentUserId, int contactId);
    Task<ServiceResult> UpdateContactChatIdAsync(int userId1, int userId2, int chatRoomId);

    Task<ServiceResult> RemoveFriendAsync(int userId, int friendId);
}
