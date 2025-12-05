using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Uchat.Server.Services.Contact;
using Uchat.Server.Services.Chat;
using Uchat.Server.Hubs;
using Uchat.Shared.DTOs;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.MongoDB;
using MongoDB.Driver;

namespace Uchat.Server.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly IChatRoomService _chatRoomService;
    private readonly IUserRepository _userRepository;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly MongoDbContext _mongoContext;

    public ContactsController(
        IContactService contactService,
        IChatRoomService chatRoomService,
        IUserRepository userRepository, 
        IHubContext<ChatHub> hubContext,
        MongoDbContext mongoContext)
    {
        _contactService = contactService;
        _chatRoomService = chatRoomService;
        _userRepository = userRepository;
        _hubContext = hubContext;
        _mongoContext = mongoContext;
    }

    [HttpPost("send-request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto)
    {
        var senderIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                 ?? User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(senderIdClaim) || !int.TryParse(senderIdClaim, out int senderId))
            return Unauthorized();

        var receiver = await _userRepository.GetByUsernameAsync(dto.Username);
        if (receiver == null)
            return NotFound(new { message = "User not found" });

        if (receiver.Id == senderId)
            return BadRequest(new { message = "You cannot add yourself" });

        // Check for existing contact/request
        var senderContactsResult = await _contactService.GetContactsAsync(senderId);
        if (senderContactsResult.Success && senderContactsResult.Data != null)
        {
            var existingContact = senderContactsResult.Data.FirstOrDefault(c => 
                c.ContactUserId == receiver.Id || c.OwnerId == receiver.Id);

            if (existingContact != null)
            {
                if (existingContact.Status == ContactStatus.Friend)
                    return BadRequest(new { message = "User is already in your contacts" });
                
                if (existingContact.Status == ContactStatus.RequestReceived)
                    return BadRequest(new { message = "This user already sent you a friend request. Please accept it from your notifications." });
                
                if (existingContact.Status == ContactStatus.RequestSent)
                    return BadRequest(new { message = "Friend request already sent to this user" });
            }
        }

        var result = await _contactService.SendFriendRequestAsync(senderId, receiver.Id);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        // Notify receiver via SignalR
        var contactsResult = await _contactService.GetContactsAsync(receiver.Id);
        if (contactsResult.Success && contactsResult.Data != null)
        {
            var newContact = contactsResult.Data.FirstOrDefault(c => 
                c.OwnerId == receiver.Id && 
                c.ContactUserId == senderId && 
                c.Status == ContactStatus.RequestReceived);
                
            if (newContact != null)
            {
                var sender = await _userRepository.GetByIdAsync(senderId);
                var contactDto = new ContactDto
                {
                    Id = newContact.Id,
                    ContactUsername = sender?.Username ?? "Unknown",
                    Nickname = sender?.DisplayName,
                    Status = ContactStatusDto.RequestReceived,
                    SavedChatRoomId = newContact.SavedChatRoomId,
                    LastMessageContent = null,
                    UnreadCount = 0
                };
                
                await _hubContext.Clients.User(receiver.Id.ToString())
                    .SendAsync("FriendRequestReceived", contactDto);
            }
        }

        return Ok(new { message = "Friend request sent successfully" });
    }

    [HttpPost("{contactId}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(int contactId)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        var contactResult = await _contactService.GetContactByIdAsync(contactId);
        if (!contactResult.Success || contactResult.Data == null)
            return NotFound(new { message = "Contact not found" });

        var contact = contactResult.Data;
        int otherUserId = (contact.OwnerId == userId) ? contact.ContactUserId : contact.OwnerId;

        // ВЫЗЫВАЕМ СЕРВИС И ПОЛУЧАЕМ ID ЧАТА СРАЗУ
        var result = await _contactService.AcceptFriendRequestAsync(userId, otherUserId);
        
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        int finalChatId = result.Data; // ID чата из сервиса

        // SignalR уведомления
        var me = await _userRepository.GetByIdAsync(userId);
        var friend = await _userRepository.GetByIdAsync(otherUserId);

        if (finalChatId > 0)
        {
            // Уведомляем друга (Requester)
            await _hubContext.Clients.User(otherUserId.ToString())
                .SendAsync("FriendRequestAccepted", new 
                { 
                    contactId, 
                    chatRoomId = finalChatId, 
                    friendUsername = me?.Username ?? "Unknown",
                    friendDisplayName = me?.DisplayName ?? "Unknown"
                });
                
            // Уведомляем себя (Accepter)
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("FriendAdded", new 
                { 
                    chatRoomId = finalChatId, 
                    friendUsername = friend?.Username ?? "Unknown",
                    friendDisplayName = friend?.DisplayName ?? "Unknown"
                });
            
            return Ok(new { message = "Friend request accepted", chatRoomId = finalChatId });
        }

        return Ok(new { message = "Friend request accepted (no chat linked)" });
    }

    [HttpPost("{contactId}/reject")]
    public async Task<IActionResult> RejectFriendRequest(int contactId)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        var result = await _contactService.RejectFriendRequestAsync(userId, contactId);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = "Friend request rejected" });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var senderIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(senderIdClaim) || !int.TryParse(senderIdClaim, out int senderId))
            return Unauthorized();

        var result = await _contactService.GetPendingRequestsAsync(senderId);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        var dtos = new List<ContactDto>();
        if (result.Data != null)
        {
            foreach (var contact in result.Data)
            {
                var requester = await _userRepository.GetByIdAsync(contact.ContactUserId);
                dtos.Add(new ContactDto
                {
                    Id = contact.Id,
                    ContactUsername = requester?.Username ?? "Unknown",
                    Nickname = requester?.DisplayName,
                    Status = ContactStatusDto.RequestReceived,
                    SavedChatRoomId = contact.SavedChatRoomId,
                    LastMessageContent = null,
                    UnreadCount = 0
                });
            }
        }

        return Ok(dtos);
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        var result = await _contactService.GetContactsAsync(userId);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        var dtos = new List<ContactDto>();
        if (result.Data != null)
        {
            foreach (var contact in result.Data)
            {
                var otherUserId = contact.OwnerId == userId ? contact.ContactUserId : contact.OwnerId;
                var user = await _userRepository.GetByIdAsync(otherUserId);
                
                dtos.Add(new ContactDto
                {
                    Id = contact.Id,
                    ContactUsername = user?.Username ?? "Unknown",
                    Nickname = user?.DisplayName,
                    Status = (ContactStatusDto)contact.Status,
                    SavedChatRoomId = contact.SavedChatRoomId,
                    LastMessageContent = await GetLastMessageContentAsync(contact.SavedChatRoomId),
                    UnreadCount = await GetUnreadCountAsync(contact.SavedChatRoomId, userId)
                });
            }
        }

        return Ok(dtos);
    }
    
    private async Task<string?> GetLastMessageContentAsync(int? chatRoomId)
    {
        if (!chatRoomId.HasValue)
            return null;
            
        var lastMessage = await _mongoContext.Messages
            .Find(m => m.ChatId == chatRoomId.Value && !m.IsDeleted)
            .SortByDescending(m => m.SentAt)
            .Limit(1)
            .FirstOrDefaultAsync();
            
        if (lastMessage == null)
            return null;
            
        if (lastMessage.Type == "system")
            return lastMessage.Content;
            
        return $"{lastMessage.Sender.DisplayName}: {lastMessage.Content}";
    }
    
    private async Task<int> GetUnreadCountAsync(int? chatRoomId, int userId)
    {
        if (!chatRoomId.HasValue)
            return 0;
            
        var count = await _mongoContext.Messages
            .CountDocumentsAsync(m => 
                m.ChatId == chatRoomId.Value && 
                !m.IsDeleted &&
                !m.ReadBy.Contains(userId));
                
        return (int)count;
    }
    
    /// <summary>
    /// Hide chat for current user (removes from member list, keeps for other participant)
    /// </summary>
    [HttpDelete("by-chat/{chatRoomId}")]
    public async Task<IActionResult> DeleteContactByChatRoom(int chatRoomId)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        // Remove friendship if exists
        var contactsResult = await _contactService.GetContactsAsync(userId);
        var contact = contactsResult.Data?.FirstOrDefault(c => c.SavedChatRoomId == chatRoomId);
        
        if (contact != null)
        {
            var friendId = (contact.OwnerId == userId) ? contact.ContactUserId : contact.OwnerId;
            await _contactService.RemoveFriendAsync(userId, friendId);
        }

        // Remove self from chat members (hides chat only for current user)
        await _chatRoomService.RemoveMemberAsync(chatRoomId, userId, userId);

        // Notify current user to update UI
        await _hubContext.Clients.User(userId.ToString())
            .SendAsync("FriendRemoved", chatRoomId);

        return Ok(new { message = "Chat removed successfully" });
    }
    
    [HttpDelete("{contactId}")]
    public async Task<IActionResult> DeleteContact(int contactId)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        var contactResult = await _contactService.GetContactByIdAsync(contactId);
        if (!contactResult.Success || contactResult.Data == null)
            return NotFound(new { message = "Contact not found" });
        
        var contact = contactResult.Data;
        var friendId = (contact.OwnerId == userId) ? contact.ContactUserId : contact.OwnerId;
        var chatRoomId = contact.SavedChatRoomId;

        var result = await _contactService.RemoveFriendAsync(userId, friendId);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        // Notify both users via SignalR
        if (chatRoomId.HasValue)
        {
            await _hubContext.Clients.User(friendId.ToString())
                .SendAsync("FriendRemoved", chatRoomId.Value);
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("FriendRemoved", chatRoomId.Value);
        }

        return Ok(new { message = "Contact deleted successfully" });
    }
}
