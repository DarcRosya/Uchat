using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;

namespace Uchat.Server.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(UchatDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        var systemUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "System");
        if (systemUser == null)
        {
            systemUser = new User 
            { 
                Username = "System", 
                Email = "system@uchat.com",
                DisplayName = "System Bot",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SYSTEM_NO_LOGIN_" + Guid.NewGuid()), // Невозможно войти
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();
            
            Console.WriteLine($"[DbInitializer] Created system user (ID: {systemUser.Id})");
        }
        else
        {
            Console.WriteLine($"[DbInitializer] System user already exists (ID: {systemUser.Id})");
        }

        var globalChat = await context.ChatRooms
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Name == "Global Chat");
            
        if (globalChat == null)
        {
            globalChat = new ChatRoom
            {
                Name = "Global Chat",
                Type = ChatRoomType.Public,
                CreatorId = systemUser.Id, 
                CreatedAt = DateTime.UtcNow,
            };
            context.ChatRooms.Add(globalChat);
            await context.SaveChangesAsync();
            
            context.ChatRoomMembers.Add(new ChatRoomMember
            {
                ChatRoomId = globalChat.Id,
                UserId = systemUser.Id,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            
            Console.WriteLine($"[DbInitializer] Created Global Chat (ID: {globalChat.Id})");
        }
        else
        {
            Console.WriteLine($"[DbInitializer] Global Chat already exists (ID: {globalChat.Id})");
        }
    }
}
