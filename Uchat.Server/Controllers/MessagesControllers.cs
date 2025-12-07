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

        var result = await _messageService.GetMessagesAsync(chatId, userId, limit, before);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> CreateMessage(
        [FromRoute] int chatId,
        [FromBody] MessageCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { Error = "Message content cannot be empty" });

        var userId = GetCurrentUserId();
        dto.SenderId = userId;
        dto.ChatRoomId = chatId;

        // 1. Проверка прав (быстрая проверка без загрузки всего графа, если есть такой метод)
        var memberCheck = await _chatRoomService.IsUserInChatAsync(userId, chatId);
        if (!memberCheck.IsSuccess) return Forbid();

        // 2. Отправка сообщения (вся логика воскрешения теперь внутри)
        var result = await _messageService.SendMessageAsync(dto);
        
        if (!result.Success)
            return BadRequest(new { Error = result.FailureReason });

        // 3. SignalR: Уведомление о "Воскрешении"
        // Сервис вернул ID того, кого вернули в чат. Уведомляем его лично.
        if (result.ResurrectedUserId.HasValue)
        {
            await _hubContext.Clients.User(result.ResurrectedUserId.Value.ToString())
                .SendAsync("ChatResurrected", new { ChatId = chatId, ResurrectedBy = userId });
        }

        // 4. Получаем полное DTO для ответа
        var message = await _messageService.GetMessageByIdAsync(result.MessageId!);
        if (message == null) return StatusCode(500, new { Error = "Message created but not found" });

        // 5. SignalR: Отправка сообщения в группу
        await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", message);

        return CreatedAtAction(nameof(GetMessageById), new { chatId, messageId = message.Id }, message);
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

