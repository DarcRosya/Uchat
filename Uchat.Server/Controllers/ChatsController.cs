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

        // Сервис сам сходит в Redis, в БД, смапит DTO и отсортирует
        // Убедись, что метод в сервисе называется GetUserChatsAsync и возвращает List<ChatRoomDto>
        var chatDtos = await _chatRoomService.GetUserChatsAsync(userId);

        return Ok(chatDtos);
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
}