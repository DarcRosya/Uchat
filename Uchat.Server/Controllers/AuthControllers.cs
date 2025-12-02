using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Uchat.Server.DTOs;
using Uchat.Server.Services.Auth;

namespace Uchat.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        
        if (result == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });
        
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
    {
        await _authService.LogoutAsync(dto.RefreshToken);
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var count = await _authService.LogoutAllAsync(userId);
        return Ok(new { message = $"Logged out from {count} devices" });
    }
}