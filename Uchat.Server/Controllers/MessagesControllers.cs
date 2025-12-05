using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Uchat.Server.Hubs;
using Uchat.Server.Services.Chat;
using Uchat.Server.Services.Messaging;
using Uchat.Shared.DTOs;
using Uchat.Database.Entities;

namespace Uchat.Server.Controllers;

[ApiController]
[Route("api/chats/{chatId}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IChatRoomService _chatRoomService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        IChatRoomService chatRoomService,
        IHubContext<ChatHub> hubContext,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _chatRoomService = chatRoomService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedMessagesDto>> GetMessages(
        [FromRoute] int chatId,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50)
    {
        if (limit < 1 || limit > 100)
        {
            return BadRequest(new { Error = "Limit is must be from 1 to 100"});
        }

        var userId = GetCurrentUserId();
        var memberCheck = await _chatRoomService.IsUserInChatAsync(userId, chatId);
        if (!memberCheck.IsSuccess)
        {
            return Forbid();
        }

        var result = await _messageService.GetMessagesAsync(chatId, limit, before);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> CreateMessage(
        [FromRoute] int chatId,
        [FromBody] MessageCreateDto dto)
    {
        // 1. Валидация
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest(new { Error = "Message content cannot be empty" });
        }

        var userId = GetCurrentUserId();
        dto.SenderId = userId;
        dto.ChatRoomId = chatId;

        // 2. Проверка прав
        var memberCheck = await _chatRoomService.IsUserInChatAsync(userId, chatId);
        if (!memberCheck.IsSuccess)
        {
            return Forbid();
        }

        // === УЛУЧШЕННАЯ ЛОГИКА ВОСКРЕШЕНИЯ (RESURRECTION V2) ===
        // 1. Получаем детали чата, включая участников
        var chatDetails = await _chatRoomService.GetChatDetailsAsync(chatId, userId);

        if (chatDetails.IsSuccess && chatDetails.ChatRoom != null && chatDetails.ChatRoom.Type == ChatRoomType.DirectMessage)
        {
            var chat = chatDetails.ChatRoom;
            int targetUserId = 0;

            // Пытаемся определить ID собеседника (того, кто НЕ мы)
            // Сначала пробуем парсить имя (15-20)
            try 
            {
                var parts = chat.Name.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int parsedId) && parsedId != userId)
                    {
                        targetUserId = parsedId;
                        break;
                    }
                }
            } catch { }

            // Если имя не помогло, ищем в истории сообщений любого "чужого" отправителя
            if (targetUserId == 0)
            {
                var oldMessages = await _messageService.GetMessagesAsync(chatId, 20); // Берем 20 последних
                if (oldMessages?.Messages != null)
                {
                    var otherMsg = oldMessages.Messages.FirstOrDefault(m => m.Sender.UserId != userId);
                    if (otherMsg != null) targetUserId = otherMsg.Sender.UserId;
                }
            }

            // Если мы нашли ID собеседника — проверяем, есть ли он в участниках
            if (targetUserId > 0)
            {
                // Проверяем список участников (Members уже загружены через Include в GetChatDetailsAsync)
                bool isTargetActive = chat.Members.Any(m => m.UserId == targetUserId);

                if (!isTargetActive)
                {
                    _logger.LogInformation($"User {targetUserId} is missing in chat {chatId}. Resurrecting...");
                    // Возвращаем его в чат
                    await _chatRoomService.AddMemberAsync(chatId, userId, targetUserId);
                    
                    // Опционально: можно отправить ему пуш "Вас вернули в чат", но SignalR ниже справится
                }
            }
        }

        // 3. Проверка ReplyTo (если это ответ)
        if (!string.IsNullOrEmpty(dto.ReplyToMessageId))
        {
            var replyToExists = await _messageService.MessageExistsAsync(
                dto.ReplyToMessageId, 
                chatId
            );
            
            if (!replyToExists)
            {
                return BadRequest(new { Error = "Reply-to message not found" });
            }
        }

        // 4. Создание сообщения
        var result = await _messageService.SendMessageAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(new { Error = result.FailureReason });
        }

        // 5. Получаем созданное сообщение
        var message = await _messageService.GetMessageByIdAsync(result.MessageId!);
        
        if (message == null)
        {
            return StatusCode(500, new { Error = "Message created but not found" });
        }

        // 6. Уведомление через SignalR
        var groupName = $"chat_{chatId}";
        _logger.LogInformation($"[SignalR] Sending ReceiveMessage to group '{groupName}': MessageID={message.Id}, Sender={message.Sender.Username}");
        
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("ReceiveMessage", message);
        
        _logger.LogInformation($"[SignalR] Message sent to group '{groupName}'");

        // 7. Возврат результата
        return CreatedAtAction(
            nameof(GetMessageById), 
            new { chatId, messageId = message.Id }, 
            message
        );
    }

    [HttpGet("{messageId}")]
    public async Task<ActionResult<MessageDto>> GetMessageById(
        [FromRoute] int chatId,
        [FromRoute] string messageId)
    {
        var userId = GetCurrentUserId();
        var memberCheck = await _chatRoomService.IsUserInChatAsync(userId, chatId);
        if (!memberCheck.IsSuccess)
        {
            return Forbid();
        }

        var message = await _messageService.GetMessageByIdAsync(messageId);
        if (message == null || message.ChatRoomId != chatId)
        {
            return NotFound();
        }

        return Ok(message);
    }


    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(
        [FromRoute] int chatId,
        [FromRoute] string messageId)
    {
        var userId = GetCurrentUserId();
        
        var result = await _messageService.DeleteMessageAsync(messageId, userId);
        
        if (!result.Success)
        {
            if (result.FailureReason?.Contains("not found") == true)
            {
                return NotFound(new { Error = result.FailureReason });
            }
            return BadRequest(new { Error = result.FailureReason });
        }

        // Уведомление через SignalR (1 параметр: messageId)
        _logger.LogInformation("[SignalR] Sending MessageDeleted to chat_{ChatId}: MessageID={MessageId}, ClearedReplies={Count}", 
            chatId, messageId, result.ClearedReplyMessageIds.Count);
        
        await _hubContext.Clients
            .Group($"chat_{chatId}")
            .SendAsync("MessageDeleted", messageId);
        
        // Если есть сообщения с очищенными reply, отправляем отдельное уведомление
        if (result.ClearedReplyMessageIds.Any())
        {
            _logger.LogInformation("[SignalR] Sending RepliesCleared for {Count} messages", result.ClearedReplyMessageIds.Count);
            
            await _hubContext.Clients
                .Group($"chat_{chatId}")
                .SendAsync("RepliesCleared", result.ClearedReplyMessageIds);
        }

        _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);
        return NoContent();
    }

    [HttpPatch("{messageId}")]
    public async Task<IActionResult> EditMessage(
        [FromRoute] int chatId,
        [FromRoute] string messageId,
        [FromBody] EditMessageDto dto)
    {
        var userId = GetCurrentUserId();
        
        var result = await _messageService.EditMessageAsync(messageId, userId, dto.Content);
        
        if (!result.Success)
        {
            if (result.FailureReason?.Contains("not found") == true)
            {
                return NotFound(new { Error = result.FailureReason });
            }
            return BadRequest(new { Error = result.FailureReason });
        }

        // Уведомление через SignalR (3 параметра: messageId, newContent, editedAt)
        var editedAt = DateTime.UtcNow;
        _logger.LogInformation("[SignalR] Sending MessageEdited to chat_{ChatId}: MessageID={MessageId}, Content={Content}", chatId, messageId, dto.Content.Substring(0, Math.Min(20, dto.Content.Length)));
        
        await _hubContext.Clients
            .Group($"chat_{chatId}")
            .SendAsync("MessageEdited", messageId, dto.Content, editedAt);

        _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);
        return NoContent();
    }

    /// <summary>
    /// Отметить сообщения как прочитанные до указанного времени
    /// </summary>
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] int chatId,
        [FromQuery] DateTime? until)
    {
        var userId = GetCurrentUserId();
        
        var memberCheck = await _chatRoomService.IsUserInChatAsync(userId, chatId);
        if (!memberCheck.IsSuccess)
        {
            return Forbid();
        }

        var untilTimestamp = until ?? DateTime.UtcNow;
        var count = await _messageService.MarkMessagesAsReadUntilAsync(chatId, userId, untilTimestamp);

        _logger.LogInformation("{Count} messages marked as read in chat {ChatId} by user {UserId}", count, chatId, userId);
        
        return Ok(new { MarkedAsRead = count });
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(claim!);
    }
}

