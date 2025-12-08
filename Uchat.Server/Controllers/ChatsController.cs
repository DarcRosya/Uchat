using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.MongoDB;
using Uchat.Server.Services.Chat;
using Uchat.Server.DTOs;
using Uchat.Shared.DTOs;
using MongoDB.Driver;
using Uchat.Server.Services.Messaging;
using Uchat.Server.Services.Unread;

namespace Uchat.Server.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IChatRoomService _chatRoomService;
    private readonly IUserRepository _userRepository;
    private readonly MongoDbContext _mongoContext;
    private readonly IUnreadCounterService _unreadCounterService;
    private readonly IMessageService _messageService;

    public ChatsController(
        IChatRoomService chatRoomService,
        IUserRepository userRepository,
        MongoDbContext mongoContext,
        IMessageService messageService,
        IUnreadCounterService unreadCounterService)
    {
        _chatRoomService = chatRoomService;
        _userRepository = userRepository;
        _mongoContext = mongoContext;
        _messageService = messageService;
        _unreadCounterService = unreadCounterService;
    }

    /// <summary>
    /// Получить список всех чатов текущего пользователя
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserChats()
    {
        var userId = GetCurrentUserId();
        var chats = await _chatRoomService.GetUserChatsAsync(userId);
        var chatDtos = new List<ChatRoomDto>();
        
        var chatIds = chats.Select(c => c.Id).ToList();

        var partnerUserIds = new HashSet<int>();
        foreach (var chat in chats.Where(c => c.Type == ChatRoomType.DirectMessage))
        {
            var partner = chat.Members?.FirstOrDefault(m => m.UserId != userId);
            if (partner != null) partnerUserIds.Add(partner.UserId);
        }

        var usersDict = (await _userRepository.GetUsersByIdsAsync(partnerUserIds.ToList()))
                    .ToDictionary(u => u.Id);

        var lastMessagesDict = await _messageService.GetLastMessagesForChatsBatch(chatIds);
        var unreadCounts = await _unreadCounterService.GetUnreadCountsAsync(userId, chatIds);

        foreach (var chat in chats)
        {
            var dto = chat.ToDto();

            if (chat.Type == ChatRoomType.DirectMessage)
            {
                var partnerId = chat.Members?.FirstOrDefault(m => m.UserId != userId)?.UserId ?? 0;
                if (partnerId > 0 && usersDict.TryGetValue(partnerId, out var partner))
                {
                    dto.Name = partner.DisplayName ?? partner.Username;
                    dto.IconUrl = partner.AvatarUrl;
                }
                else
                {
                    dto.Name = "Uknown User"; // Или старое имя чата как фоллбэк
                }
            }

            if (lastMessagesDict.TryGetValue(chat.Id, out var lastMsgDto))
            {
                if (string.IsNullOrEmpty(lastMsgDto.Content) && lastMsgDto.Attachments.Any())
                {
                    var firstAtt = lastMsgDto.Attachments.First();
                    
                    dto.LastMessageContent = GetAttachmentPreview(firstAtt);
                }
                else
                {
                    dto.LastMessageContent = lastMsgDto.Content;
                }

                dto.LastMessageAt = lastMsgDto.SentAt;
            }
            else
            {
                dto.LastMessageAt = chat.CreatedAt;
                dto.LastMessageContent = "";
            }
            
            dto.UnreadCount = unreadCounts.TryGetValue(chat.Id, out var unread) ? unread : 0;
            chatDtos.Add(dto);
        }

        // Сортируем уже в памяти перед отдачей
        return Ok(chatDtos.OrderByDescending(x => x.LastMessageAt));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetChatById(int id)
    {
        var userId = GetCurrentUserId();
        var result = await _chatRoomService.GetChatDetailsAsync(id, userId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ChatErrorType.NotFound => NotFound(new { error = "Chat not found" }),
                ChatErrorType.Forbidden => Forbid(),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(result.ChatRoom!.ToDetailDto());
    }

    [HttpPost]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequestDto request)
    {
        if (!ModelState.IsValid) 
            return BadRequest(ModelState);

        // Валидация типа чата
        if (!Enum.TryParse<ChatRoomType>(request.Type, true, out var type))
            return BadRequest(new { error = "Invalid chat type" });

        var userId = GetCurrentUserId();
        var result = await _chatRoomService.CreateChatAsync(
            userId, 
            request.Name, 
            type, 
            request.Description, 
            request.InitialMemberIds
        );

        if (!result.IsSuccess) 
            return BadRequest(new { error = result.ErrorMessage });

        var chatDto = result.ChatRoom!.ToDto();
        return CreatedAtAction(nameof(GetChatById), new { id = chatDto.Id }, chatDto);
    }

    /// <summary>
    /// Добавить участника в чат
    /// </summary>
    [HttpPost("{chatId}/members")]
    public async Task<IActionResult> AddMember(int chatId, [FromBody] AddMemberRequestDto request)
    {
        if (!ModelState.IsValid) 
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        var result = await _chatRoomService.AddMemberAsync(chatId, userId, request.UserId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ChatErrorType.NotFound => NotFound(new { error = "Chat not found" }),
                ChatErrorType.Forbidden => Forbid(),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Member added successfully" });
    }

    /// <summary>
    /// Удалить участника из чата
    /// </summary>
    [HttpDelete("{chatId}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int chatId, int memberId)
    {
        var userId = GetCurrentUserId();
        var result = await _chatRoomService.RemoveMemberAsync(chatId, userId, memberId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ChatErrorType.NotFound => NotFound(new { error = "Chat not found" }),
                ChatErrorType.Forbidden => Forbid(),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Member removed successfully" });
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(claim!);
    }

    private string GetAttachmentPreview(Shared.DTOs.MessageAttachment attachment)
    {
        if (attachment == null) return "";

        var mime = attachment.ContentType?.ToLower() ?? "";
        var fileName = attachment.FileName;

        if (mime.Contains("gif")) 
            return "👾 GIF";

        if (mime.StartsWith("image")) 
            return $"📷 {fileName}"; 

        if (mime.StartsWith("video")) 
            return $"🎥 {fileName}"; 

        // 
        // if (mime.StartsWith("audio"))
        //     return "🎤 Voice message";

        // 5. Обычные файлы (документы, архивы, код)
        // Тут мы показываем скрепку + ИМЯ ФАЙЛА, как ты и хотел
        return $"📎 {fileName}"; 
    }
}