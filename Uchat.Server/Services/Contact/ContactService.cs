using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.MongoDB;
using Uchat.Shared.DTOs;

namespace Uchat.Server.Services.Contact;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly UchatDbContext _context;
    private readonly MongoDbContext _mongoContext;

    public ContactService(
        IContactRepository contactRepository,
        IUserRepository userRepository,
        IChatRoomRepository chatRoomRepository,
        UchatDbContext context,
        MongoDbContext mongoContext)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _chatRoomRepository = chatRoomRepository;
        _context = context;
        _mongoContext = mongoContext;
    }

    public async Task<ServiceResult> SendFriendRequestAsync(int senderId, int receiverId)
    {
        if (senderId == receiverId)
            return ServiceResult.Failure("You cannot send friend request to yourself");

        var receiver = await _userRepository.GetByIdAsync(receiverId);
        if (receiver == null)
            return ServiceResult.Failure("User not found");

        // Check if a connection already exists
        var existingSender = await _contactRepository.FindContactAsync(senderId, receiverId);
        var existingReceiver = await _contactRepository.FindContactAsync(receiverId, senderId);

        // If already friends
        if (existingSender?.Status == ContactStatus.Friend)
            return ServiceResult.Failure("Already friends");

        // If a request has already been sent
        if (existingSender?.Status == ContactStatus.RequestSent)
            return ServiceResult.Failure("Friend request already sent");

        // If the receiver has blocked the sender
        if (existingReceiver?.IsBlocked == true)
            return ServiceResult.Failure("Cannot send request to this user");

        // Use transaction for atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create/update sender record
            if (existingSender == null)
            {
                await _contactRepository.AddContactAsync(senderId, receiverId);
                existingSender = await _contactRepository.FindContactAsync(senderId, receiverId);
            }
            
            await _contactRepository.UpdateStatusAsync(existingSender!.Id, ContactStatus.RequestSent);

            // Create/update receiver record
            if (existingReceiver == null)
            {
                await _contactRepository.AddContactAsync(receiverId, senderId);
                existingReceiver = await _contactRepository.FindContactAsync(receiverId, senderId);
            }
            
            await _contactRepository.UpdateStatusAsync(existingReceiver!.Id, ContactStatus.RequestReceived);

            await transaction.CommitAsync();
            return ServiceResult.SuccessResult();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while sending friend request");
        }
    }

    public async Task<ServiceResult<int>> AcceptFriendRequestAsync(int userId, int requesterId)
    {
        var userContact = await _contactRepository.FindContactAsync(userId, requesterId);
        var requesterContact = await _contactRepository.FindContactAsync(requesterId, userId);

        if (userContact == null || requesterContact == null)
            return ServiceResult<int>.Failure("Friend request not found");

        if (userContact.Status != ContactStatus.RequestReceived)
            return ServiceResult<int>.Failure("No pending friend request from this user");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Обновляем статус дружбы
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.Friend);
            await _contactRepository.UpdateStatusAsync(requesterContact.Id, ContactStatus.Friend);

            int finalChatId = 0;

            // 2. Логика Чата (Zombie Fix + Creation)
            if (userContact.SavedChatRoomId.HasValue)
            {
                // === СЦЕНАРИЙ А: Чат уже был (Восстанавливаем участников) ===
                finalChatId = userContact.SavedChatRoomId.Value;

                // Проверяем текущего юзера (Accepter)
                bool isUserMember = await _context.ChatRoomMembers
                    .AnyAsync(m => m.ChatRoomId == finalChatId && m.UserId == userId);
                
                if (!isUserMember)
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                    {
                        ChatRoomId = finalChatId,
                        UserId = userId,
                        JoinedAt = DateTime.UtcNow
                    });
                }

                // Проверяем друга (Requester)
                bool isRequesterMember = await _context.ChatRoomMembers
                    .AnyAsync(m => m.ChatRoomId == finalChatId && m.UserId == requesterId);

                if (!isRequesterMember)
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                    {
                        ChatRoomId = finalChatId,
                        UserId = requesterId,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // === СЦЕНАРИЙ Б: Чата нет (Создаем новый) ===
                var chatRoom = new ChatRoom
                {
                    Name = $"{userId}_{requesterId}",
                    Type = ChatRoomType.DirectMessage,
                    CreatorId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                
                chatRoom = await _chatRoomRepository.CreateAsync(chatRoom);
                finalChatId = chatRoom.Id;
                
                // Добавляем обоих
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = finalChatId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                });
                
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = finalChatId,
                    UserId = requesterId,
                    JoinedAt = DateTime.UtcNow
                });
                
                // Сохраняем ID чата в контакты
                await _contactRepository.SetSavedChatRoomAsync(userContact.Id, finalChatId);
                await _contactRepository.SetSavedChatRoomAsync(requesterContact.Id, finalChatId);
            }

            await transaction.CommitAsync();
            
            // Возвращаем ID чата
            return ServiceResult<int>.SuccessResult(finalChatId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Logger.Error("Failed to accept friend request", ex);
            return ServiceResult<int>.Failure("Database error occurred while accepting friend request");
        }
    }

    public async Task<ServiceResult> RejectFriendRequestAsync(int userId, int requesterId)
    {
        var userContact = await _contactRepository.FindContactAsync(userId, requesterId);
        var requesterContact = await _contactRepository.FindContactAsync(requesterId, userId);

        if (userContact == null)
            return ServiceResult.Failure("Friend request not found");

        if (userContact.Status != ContactStatus.RequestReceived)
            return ServiceResult.Failure("No pending friend request from this user");

        // Use transaction for atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Do not delete the contact (preserve chat history and settings), 
            // instead reset status to None
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            
            if (requesterContact != null)
                await _contactRepository.UpdateStatusAsync(requesterContact.Id, ContactStatus.None);

            await transaction.CommitAsync();
            return ServiceResult.SuccessResult();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while rejecting friend request");
        }
    }

    public async Task<ServiceResult> RemoveFriendAsync(int userId, int friendId)
    {
        var userContact = await _contactRepository.FindContactAsync(userId, friendId);
        var friendContact = await _contactRepository.FindContactAsync(friendId, userId);

        if (userContact == null || friendContact == null)
            return ServiceResult.Failure("Contact not found");

        if (userContact.Status != ContactStatus.Friend)
            return ServiceResult.Failure("Not friends with this user");

        // Use transaction for atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Revert status to None (contacts remain, but no longer friends)
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            await _contactRepository.UpdateStatusAsync(friendContact.Id, ContactStatus.None);

            await transaction.CommitAsync();
            return ServiceResult.SuccessResult();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while removing friend");
        }
    }

    public async Task<ServiceResult> UpdateContactChatIdAsync(int userId1, int userId2, int chatRoomId)
    {
        try
        {
            // Обновляем запись для первого пользователя
            var contact1 = await _contactRepository.FindContactAsync(userId1, userId2);
            if (contact1 != null)
            {
                await _contactRepository.SetSavedChatRoomAsync(contact1.Id, chatRoomId);
            }

            // Обновляем запись для второго пользователя
            var contact2 = await _contactRepository.FindContactAsync(userId2, userId1);
            if (contact2 != null)
            {
                await _contactRepository.SetSavedChatRoomAsync(contact2.Id, chatRoomId);
            }

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to update contact chat ID for users {userId1} and {userId2}", ex);
            return ServiceResult.Failure("Database error occurred while updating contact chat ID");
        }
    }

    public async Task<ServiceResult> BlockUserAsync(int userId, int blockedUserId)
    {
        if (userId == blockedUserId)
            return ServiceResult.Failure("You cannot block yourself");

        var userContact = await _contactRepository.FindContactAsync(userId, blockedUserId);
        
        if (userContact == null)
        {
            // Create record if it doesn't exist
            await _contactRepository.AddContactAsync(userId, blockedUserId);
            userContact = await _contactRepository.FindContactAsync(userId, blockedUserId);
        }

        if (userContact!.IsBlocked)
            return ServiceResult.Failure("User is already blocked");

        // Use transaction for atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Current user: Status Blocked + IsBlocked flag
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.Blocked);
            await _contactRepository.SetBlockedAsync(userContact.Id, true);

            // 2. Blocked user: sever friendship (if friends)
            var blockedUserContact = await _contactRepository.FindContactAsync(blockedUserId, userId);
            if (blockedUserContact != null && blockedUserContact.Status == ContactStatus.Friend)
            {
                // Reset status to None (do not delete to preserve history)
                await _contactRepository.UpdateStatusAsync(blockedUserContact.Id, ContactStatus.None);
            }

            await transaction.CommitAsync();
            return ServiceResult.SuccessResult();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while blocking user");
        }
    }

    public async Task<ServiceResult> UnblockUserAsync(int userId, int blockedUserId)
    {
        var userContact = await _contactRepository.FindContactAsync(userId, blockedUserId);

        if (userContact == null)
            return ServiceResult.Failure("Contact not found");

        if (userContact.Status != ContactStatus.Blocked)
            return ServiceResult.Failure("User is not blocked");

        // Use transaction for consistency
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Revert to None
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            await _contactRepository.SetBlockedAsync(userContact.Id, false);

            await transaction.CommitAsync();
            return ServiceResult.SuccessResult();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while unblocking user");
        }
    }

    public async Task<IEnumerable<Database.Entities.Contact>> GetFriendsAsync(int userId)
    {
        return await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.Friend);
    }

    public async Task<IEnumerable<Database.Entities.Contact>> GetIncomingRequestsAsync(int userId)
    {
        return await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.RequestReceived);
    }

    public async Task<IEnumerable<Database.Entities.Contact>> GetOutgoingRequestsAsync(int userId)
    {
        return await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.RequestSent);
    }

    public async Task<IEnumerable<Database.Entities.Contact>> GetBlockedUsersAsync(int userId)
    {
        return await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.Blocked);
    }

    public async Task<ServiceResult<Database.Entities.Contact>> GetContactByIdAsync(int contactId)
    {
        var contact = await _context.Contacts.FindAsync(contactId);
        
        if (contact == null)
            return ServiceResult<Database.Entities.Contact>.Failure("Contact not found");
            
        return ServiceResult<Database.Entities.Contact>.SuccessResult(contact);
    }

    public async Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetContactsAsync(int userId)
    {
        var contacts = await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.Friend);
        return ServiceResult<IEnumerable<Database.Entities.Contact>>.SuccessResult(contacts);
    }

    public async Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetPendingRequestsAsync(int userId)
    {
        var requests = await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.RequestReceived);
        return ServiceResult<IEnumerable<Database.Entities.Contact>>.SuccessResult(requests);
    }

    public async Task<ServiceResult> SetFavoriteAsync(int userId, int contactUserId, bool isFavorite)
    {
        var contact = await _contactRepository.FindContactAsync(userId, contactUserId);
        
        if (contact == null)
            return ServiceResult.Failure("Contact not found");

        await _contactRepository.SetFavoriteAsync(contact.Id, isFavorite);
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult> SetNicknameAsync(int userId, int contactUserId, string? nickname)
    {
        var contact = await _contactRepository.FindContactAsync(userId, contactUserId);
        
        if (contact == null)
            return ServiceResult.Failure("Contact not found");

        await _contactRepository.SetNicknameAsync(contact.Id, nickname);
        return ServiceResult.SuccessResult();
    }
}
