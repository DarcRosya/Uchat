using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Chat;
using Uchat.Server.DTOs;

namespace Uchat.Server.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IChatRoomService _chatRoomService;
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatsController(
        IChatRoomService chatRoomService,
        IChatRoomRepository chatRoomRepository)
    {
        _chatRoomService = chatRoomService;
        _chatRoomRepository = chatRoomRepository;
    }

    /// <summary>
    /// Get all chat rooms for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserChats()
    {
        var userId = GetCurrentUserId();
        var chats = await _chatRoomRepository.GetUserChatRoomsAsync(userId);
        
        var chatDtos = chats.Select(c => new ChatRoomDto
        {
            Id = c.Id,
            Name = c.Name ?? string.Empty,
            Description = c.Description,
            IconUrl = c.IconUrl,
            Type = c.Type.ToString(),
            CreatorId = c.CreatorId,
            CreatedAt = c.CreatedAt,
            MemberCount = c.Members.Count
        });

        return Ok(chatDtos);
    }

    /// <summary>
    /// Get specific chat room by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetChatById(int id)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatRoomRepository.GetByIdAsync(id);

        if (chat == null)
            return NotFound(new { error = "Chat not found" });

        // Check if user is a member
        if (!chat.Members.Any(m => m.UserId == userId))
            return Forbid();

        var chatDto = new ChatRoomDetailDto
        {
            Id = chat.Id,
            Name = chat.Name ?? string.Empty,
            Description = chat.Description,
            IconUrl = chat.IconUrl,
            Type = chat.Type.ToString(),
            CreatorId = chat.CreatorId,
            CreatedAt = chat.CreatedAt,
            ParentChatRoomId = chat.ParentChatRoomId,
            MaxMembers = chat.MaxMembers,
            DefaultCanSendMessages = chat.DefaultCanSendMessages ?? true,
            DefaultCanInviteMembers = chat.DefaultCanInviteUsers ?? false,
            SlowModeSeconds = chat.SlowModeSeconds,
            Members = chat.Members.Select(m => new ChatMemberDto
            {
                UserId = m.UserId,
                Username = m.User?.Username ?? "Unknown",
                Role = m.Role.ToString(),
                JoinedAt = m.JoinedAt
            }).ToList()
        };

        return Ok(chatDto);
    }

    /// <summary>
    /// Create a new chat room
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { error = string.Join(", ", errors) });
        }

        var userId = GetCurrentUserId();

        var dto = new CreateChatDto
        {
            CreatorId = userId,
            Name = request.Name,
            Type = Enum.Parse<ChatRoomType>(request.Type, true),
            Description = request.Description,
            IconUrl = request.IconUrl,
            InitialMemberIds = request.InitialMemberIds ?? Array.Empty<int>(),
            ParentChatRoomId = request.ParentChatRoomId,
            MaxMembers = request.MaxMembers
        };

        var result = await _chatRoomService.CreateChatAsync(dto);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var chatDto = new ChatRoomDto
        {
            Id = result.ChatRoom!.Id,
            Name = result.ChatRoom.Name ?? string.Empty,
            Description = result.ChatRoom.Description,
            IconUrl = result.ChatRoom.IconUrl,
            Type = result.ChatRoom.Type.ToString(),
            CreatorId = result.ChatRoom.CreatorId,
            CreatedAt = result.ChatRoom.CreatedAt,
            MemberCount = result.ChatRoom.Members.Count
        };

        return CreatedAtAction(nameof(GetChatById), new { id = chatDto.Id }, chatDto);
    }

    /// <summary>
    /// Add a member to a chat room
    /// </summary>
    [HttpPost("{chatId}/members")]
    public async Task<IActionResult> AddMember(int chatId, [FromBody] AddMemberRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        var role = string.IsNullOrEmpty(request.Role) 
            ? ChatRoomRole.Member 
            : Enum.Parse<ChatRoomRole>(request.Role, true);

        var result = await _chatRoomService.AddMemberAsync(chatId, userId, request.UserId, role);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Member added successfully" });
    }

    /// <summary>
    /// Remove a member from a chat room
    /// </summary>
    [HttpDelete("{chatId}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int chatId, int memberId)
    {
        var userId = GetCurrentUserId();
        var result = await _chatRoomService.RemoveMemberAsync(chatId, userId, memberId);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Member removed successfully" });
    }

    /// <summary>
    /// Update member role in a chat room
    /// </summary>
    [HttpPut("{chatId}/members/{memberId}/role")]
    public async Task<IActionResult> UpdateMemberRole(int chatId, int memberId, [FromBody] UpdateRoleRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        var newRole = Enum.Parse<ChatRoomRole>(request.Role, true);

        var result = await _chatRoomService.UpdateMemberRoleAsync(chatId, userId, memberId, newRole);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Role updated successfully" });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(userIdClaim!);
    }
}
