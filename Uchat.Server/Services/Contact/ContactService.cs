using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Server.Services.Contact;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly UchatDbContext _context;

    public ContactService(
        IContactRepository contactRepository,
        IUserRepository userRepository,
        IChatRoomRepository chatRoomRepository,
        UchatDbContext context)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _chatRoomRepository = chatRoomRepository;
        _context = context;
    }

    public async Task<ServiceResult> SendFriendRequestAsync(int senderId, int receiverId)
    {
        if (senderId == receiverId)
            return ServiceResult.Failure("You cannot send friend request to yourself");

        var receiver = await _userRepository.GetByIdAsync(receiverId);
        if (receiver == null)
            return ServiceResult.Failure("User not found");

        // Проверяем существует ли уже связь
        var existingSender = await _contactRepository.FindContactAsync(senderId, receiverId);
        var existingReceiver = await _contactRepository.FindContactAsync(receiverId, senderId);

        // Если уже друзья
        if (existingSender?.Status == ContactStatus.Friend)
            return ServiceResult.Failure("Already friends");

        // Если уже есть заявка
        if (existingSender?.Status == ContactStatus.RequestSent)
            return ServiceResult.Failure("Friend request already sent");

        // Если получатель заблокировал отправителя
        if (existingReceiver?.IsBlocked == true)
            return ServiceResult.Failure("Cannot send request to this user");

        // Используем транзакцию для атомарности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Создаем/обновляем запись отправителя
            if (existingSender == null)
            {
                await _contactRepository.AddContactAsync(senderId, receiverId);
                existingSender = await _contactRepository.FindContactAsync(senderId, receiverId);
            }
            
            await _contactRepository.UpdateStatusAsync(existingSender!.Id, ContactStatus.RequestSent);

            // Создаем/обновляем запись получателя
            if (existingReceiver == null)
            {
                await _contactRepository.AddContactAsync(receiverId, senderId);
                existingReceiver = await _contactRepository.FindContactAsync(receiverId, senderId);
            }
            
            await _contactRepository.UpdateStatusAsync(existingReceiver!.Id, ContactStatus.RequestReceived);

            await transaction.CommitAsync();
            return ServiceResult.Success();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while sending friend request");
        }
    }

    public async Task<ServiceResult> AcceptFriendRequestAsync(int userId, int requesterId)
    {
        var userContact = await _contactRepository.FindContactAsync(userId, requesterId);
        var requesterContact = await _contactRepository.FindContactAsync(requesterId, userId);

        if (userContact == null || requesterContact == null)
            return ServiceResult.Failure("Friend request not found");

        if (userContact.Status != ContactStatus.RequestReceived)
            return ServiceResult.Failure("No pending friend request from this user");

        // Используем транзакцию для атомарности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Обновляем обе записи на Friend
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.Friend);
            await _contactRepository.UpdateStatusAsync(requesterContact.Id, ContactStatus.Friend);

            // Создаем приватный чат, если его еще нет
            if (userContact.SavedChatRoomId == null)
            {
                var chatRoom = new ChatRoom
                {
                    Name = $"Private Chat {userId}-{requesterId}",
                    Type = ChatRoomType.DirectMessage,
                    CreatorId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                
                chatRoom = await _chatRoomRepository.CreateAsync(chatRoom);
                
                // Добавляем обоих пользователей в чат
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = chatRoom.Id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                });
                
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = chatRoom.Id,
                    UserId = requesterId,
                    JoinedAt = DateTime.UtcNow
                });
                
                // Сохраняем ID чата в оба контакта
                await _contactRepository.SetSavedChatRoomAsync(userContact.Id, chatRoom.Id);
                await _contactRepository.SetSavedChatRoomAsync(requesterContact.Id, chatRoom.Id);
            }

            await transaction.CommitAsync();
            return ServiceResult.Success();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while accepting friend request");
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

        // Используем транзакцию для атомарности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Не удаляем контакт (сохраняем историю переписки и настройки),
            // а сбрасываем статус в None
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            
            if (requesterContact != null)
                await _contactRepository.UpdateStatusAsync(requesterContact.Id, ContactStatus.None);

            await transaction.CommitAsync();
            return ServiceResult.Success();
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

        // Используем транзакцию для атомарности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Возвращаем статус на None (остаются контакты, но не друзья)
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            await _contactRepository.UpdateStatusAsync(friendContact.Id, ContactStatus.None);

            await transaction.CommitAsync();
            return ServiceResult.Success();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure("Database error occurred while removing friend");
        }
    }

    public async Task<ServiceResult> BlockUserAsync(int userId, int blockedUserId)
    {
        if (userId == blockedUserId)
            return ServiceResult.Failure("You cannot block yourself");

        var userContact = await _contactRepository.FindContactAsync(userId, blockedUserId);
        
        if (userContact == null)
        {
            // Создаем запись если её нет
            await _contactRepository.AddContactAsync(userId, blockedUserId);
            userContact = await _contactRepository.FindContactAsync(userId, blockedUserId);
        }

        if (userContact!.IsBlocked)
            return ServiceResult.Failure("User is already blocked");

        // Используем транзакцию для атомарности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. У меня: Статус Blocked + Флаг IsBlocked
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.Blocked);
            await _contactRepository.SetBlockedAsync(userContact.Id, true);

            // 2. У того, кого заблокировали: разрываем дружбу (если были друзьями)
            var blockedUserContact = await _contactRepository.FindContactAsync(blockedUserId, userId);
            if (blockedUserContact != null && blockedUserContact.Status == ContactStatus.Friend)
            {
                // Сбрасываем статус на None (не удаляем, чтобы сохранить историю)
                await _contactRepository.UpdateStatusAsync(blockedUserContact.Id, ContactStatus.None);
            }

            await transaction.CommitAsync();
            return ServiceResult.Success();
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

        // Используем транзакцию для консистентности
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Возвращаем на None
            await _contactRepository.UpdateStatusAsync(userContact.Id, ContactStatus.None);
            await _contactRepository.SetBlockedAsync(userContact.Id, false);

            await transaction.CommitAsync();
            return ServiceResult.Success();
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

    public async Task<ServiceResult> SetFavoriteAsync(int userId, int contactUserId, bool isFavorite)
    {
        var contact = await _contactRepository.FindContactAsync(userId, contactUserId);
        
        if (contact == null)
            return ServiceResult.Failure("Contact not found");

        await _contactRepository.SetFavoriteAsync(contact.Id, isFavorite);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetNicknameAsync(int userId, int contactUserId, string? nickname)
    {
        var contact = await _contactRepository.FindContactAsync(userId, contactUserId);
        
        if (contact == null)
            return ServiceResult.Failure("Contact not found");

        await _contactRepository.SetNicknameAsync(contact.Id, nickname);
        return ServiceResult.Success();
    }
}
