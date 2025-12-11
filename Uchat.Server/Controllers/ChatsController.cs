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

    [HttpGet]
    public async Task<IActionResult> GetUserChats()
    {
        var userId = GetCurrentUserId();

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

    [HttpGet("invites/pending")]
    public async Task<ActionResult<List<ChatRoomDto>>> GetPendingInvites()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var invites = await _chatRoomService.GetPendingGroupInvitesAsync(userId);
        return Ok(invites);
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
            request.InitialMemberIds
        );

        if (!result.IsSuccess) 
            return BadRequest(new { error = result.ErrorMessage });

        var chatDto = result.ChatRoom!.ToDto();
        return CreatedAtAction(nameof(GetChatById), new { id = chatDto.Id }, chatDto);
    }

    [HttpPost("{chatId}/members")]
    public async Task<IActionResult> AddMember(int chatId, [FromBody] AddMemberRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { error = "Username is required" });

        var userToAdd = await _userRepository.GetByUsernameAsync(request.Username);
        if (userToAdd == null)
        {
            return NotFound(new { error = $"User '{request.Username}' not found" });
        }

        var currentUserId = GetCurrentUserId();
        
        var result = await _chatRoomService.AddMemberAsync(chatId, currentUserId, userToAdd.Id);

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

    [HttpPut("{chatId}/pin")]
    public async Task<IActionResult> SetChatPin(int chatId, [FromBody] PinRequestDto dto)
    {
        var userId = GetCurrentUserId();

        var result = await _chatRoomService.SetGroupPinAsync(userId, chatId, dto.IsPinned);

        if (!result.IsSuccess)
            return BadRequest(result.ErrorMessage);

        return Ok(new { message = dto.IsPinned ? "Pinned" : "Unpinned" });
    }

    [HttpPost("{chatId}/leave")]
    public async Task<IActionResult> LeaveChat(int chatId)
    {
        var userId = GetCurrentUserId();

        var result = await _chatRoomService.RemoveMemberAsync(chatId, userId, userId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "You left the chat" });
    }

    [HttpPut("{chatId}")]
    public async Task<IActionResult> UpdateChat(int chatId, [FromBody] UpdateChatDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _chatRoomService.UpdateChatNameAsync(chatId, userId, request.Name);

        if (!result.IsSuccess) return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Chat updated" });
    }

    [HttpPost("{chatId}/accept")]
    public async Task<IActionResult> AcceptInvite(int chatId)
    {
        var userId = GetCurrentUserId(); 
        
        var result = await _chatRoomService.AcceptInviteAsync(chatId, userId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Invite accepted", chat = result.ChatRoom!.ToDto() });
    }

    [HttpPost("{chatId}/reject")]
    public async Task<IActionResult> RejectInvite(int chatId)
    {
        var userId = GetCurrentUserId();
        var result = await _chatRoomService.RejectInviteAsync(chatId, userId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ChatErrorType.NotFound => NotFound(new { error = "Invite not found" }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Invite rejected" });
    }

    [HttpPost("join/{name}")]
    public async Task<IActionResult> JoinPublicGroupByName(string name)
    {
        var userId = GetCurrentUserId();
        
        var result = await _chatRoomService.JoinPublicChatByNameAsync(name, userId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ChatErrorType.NotFound => NotFound(new { error = "Public group not found" }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(result.ChatRoom!.ToDto());
    }

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