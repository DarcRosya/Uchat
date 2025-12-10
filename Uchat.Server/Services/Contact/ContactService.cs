using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.MongoDB;
using Uchat.Server.Services.Redis;
using Uchat.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Uchat.Shared;
using Microsoft.AspNetCore.SignalR;
using Uchat.Server.Hubs;

namespace Uchat.Server.Services.Contact;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly UchatDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IRedisService _redisService;

    private static readonly TimeSpan SortedSetEntryTtl = TimeSpan.FromHours(24);

    public ContactService(
        IContactRepository contactRepository,
        IUserRepository userRepository,
        IChatRoomRepository chatRoomRepository,
        UchatDbContext context,
        IHubContext<ChatHub> hubContext,
        MongoDbContext mongoContext,
        IRedisService redisService)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _chatRoomRepository = chatRoomRepository;
        _context = context;
        _hubContext = hubContext;
        _mongoContext = mongoContext;
        _redisService = redisService;
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

        if (existingSender != null && existingSender.FriendRequestRejectedAt.HasValue)
        {
            var cooldownPeriod = TimeSpan.FromMinutes(1);
            var timePassed = DateTime.UtcNow - existingSender.FriendRequestRejectedAt.Value;

            if (timePassed < cooldownPeriod)
            {
                var timeLeft = cooldownPeriod - timePassed;
                return ServiceResult.Failure($"User rejected your request recently. You can try again in {timeLeft.Seconds}.");
            }
            
            existingSender.FriendRequestRejectedAt = null; 
        }

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

            if (existingSender!.FriendRequestRejectedAt != null) 
            {
                existingSender.FriendRequestRejectedAt = null;
                _context.Contacts.Update(existingSender); 
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

            try 
            {
                // Нам нужно получить данные отправителя, чтобы показать имя в уведомлении
                var senderUser = await _userRepository.GetByIdAsync(senderId);

                var notificationData = new ContactDto
                {
                    OwnerId = receiverId, 
                    ContactUserId = senderId,             
                    ContactUsername = senderUser.Username, 
                    Nickname = senderUser.Username,      
                    Status = ContactStatusDto.RequestReceived,  
                    LastMessageContent = "Friend request received"
                };

                // Отправляем уведомление получателю (receiverId)
                await _hubContext.Clients.User(receiverId.ToString())
                    .SendAsync("FriendRequestReceived", notificationData);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send SignalR notification for friend request", ex);
                // Не прерываем выполнение, так как в БД уже записалось
            }

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

                var existingMembers = await _context.ChatRoomMembers
                .IgnoreQueryFilters()
                .Where(m => m.ChatRoomId == finalChatId && (m.UserId == userId || m.UserId == requesterId))
                .ToListAsync();
                
                var currentUserMember = existingMembers.FirstOrDefault(m => m.UserId == userId);
                if (currentUserMember != null)
                {
                    // Если был удален - воскрешаем
                    if (currentUserMember.IsDeleted)
                    {
                        currentUserMember.IsDeleted = false;
                        currentUserMember.ClearedHistoryAt = DateTime.UtcNow; 
                    }
                }
                else
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                    {
                        ChatRoomId = finalChatId, UserId = userId, JoinedAt = DateTime.UtcNow
                    });
                }

                var friendMember = existingMembers.FirstOrDefault(m => m.UserId == requesterId);
                if (friendMember != null)
                {
                    if (friendMember.IsDeleted)
                    {
                        friendMember.IsDeleted = false;
                        friendMember.ClearedHistoryAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                    {
                        ChatRoomId = finalChatId, UserId = requesterId, JoinedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                // === СЦЕНАРИЙ Б: В чате больше никого нет - (Создаем новый) ===
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
            await TrackChatSortedSetForUsersAsync(finalChatId, new[] { userId, requesterId }, DateTime.UtcNow);

            var acceptorUser = await _userRepository.GetByIdAsync(userId);

            var notificationData = new 
            { 
                contactId = userId, // ID того, кто принял
                chatRoomId = finalChatId,
                friendDisplayName = acceptorUser.Username,
                Type = "DirectMessage"
            };

            // Отправляем тому, кто кидал заявку (requesterId)
            await _hubContext.Clients.User(requesterId.ToString())
                .SendAsync("FriendRequestAccepted", notificationData);
            
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

    public async Task<ServiceResult<int>> RejectFriendRequestAsync(int currentUserId, int requesterId)
    {
        var userContact = await _contactRepository.FindContactAsync(currentUserId, requesterId);

        if (userContact == null)
            return ServiceResult<int>.Failure("Contact request not found");

        if (userContact.OwnerId != currentUserId)
            return ServiceResult<int>.Failure("This is not your contact request");

        if (userContact.Status != ContactStatus.RequestReceived)
            return ServiceResult<int>.Failure("No pending friend request found");

        var requesterContact = await _contactRepository.FindContactAsync(requesterId, currentUserId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            userContact.Status = ContactStatus.None;

            if (requesterContact != null)
            {
                requesterContact.Status = ContactStatus.None;
                requesterContact.FriendRequestRejectedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<int>.SuccessResult(requesterId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ServiceResult<int>.Failure("Database error");
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
            var savedChatIds = new[] { userContact.SavedChatRoomId, friendContact.SavedChatRoomId }
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (savedChatIds.Any())
            {
                await RemoveChatFromUsersSortedSetAsync(savedChatIds, new[] { userId, friendId });
            }

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

    public async Task<ServiceResult<IEnumerable<Database.Entities.Contact>>> GetPendingRequestsAsync(int userId)
    {
        var requests = await _contactRepository.GetContactsByStatusAsync(userId, ContactStatus.RequestReceived);
        return ServiceResult<IEnumerable<Database.Entities.Contact>>.SuccessResult(requests);
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

    private async Task TrackChatSortedSetForUsersAsync(int chatId, IEnumerable<int> userIds, DateTime lastActivity)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var targets = userIds.Distinct().ToList();
        if (!targets.Any())
        {
            return;
        }

        var score = new DateTimeOffset(lastActivity.ToUniversalTime()).ToUnixTimeSeconds();
        var tasks = targets.Select(userId => _redisService.UpdateSortedSetAsync(
            RedisCacheKeys.GetUserChatSortedSetKey(userId),
            chatId.ToString(),
            score,
            SortedSetEntryTtl));

        await Task.WhenAll(tasks);
    }

    private async Task RemoveChatFromUsersSortedSetAsync(IEnumerable<int> chatIds, IEnumerable<int> userIds)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var targets = chatIds.Distinct().ToList();
        if (!targets.Any())
        {
            return;
        }

        var users = userIds.Distinct().ToList();
        if (!users.Any())
        {
            return;
        }

        var tasks = targets.SelectMany(chatId => users.Select(userId =>
            _redisService.RemoveSortedSetMemberAsync(RedisCacheKeys.GetUserChatSortedSetKey(userId), chatId.ToString())));

        await Task.WhenAll(tasks);
    }
}
