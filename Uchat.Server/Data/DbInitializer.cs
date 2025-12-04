using Microsoft.EntityFrameworkCore;
using Uchat.Database.Context;
using Uchat.Database.Entities;

namespace Uchat.Server.Data;

/// <summary>
/// Инициализатор базы данных - создает обязательные системные сущности при старте приложения.
/// Решает проблему race condition и хардкода ID.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(UchatDbContext context)
    {
        // 1. Убеждаемся, что база создана (миграции уже применены в Program.cs)
        await context.Database.EnsureCreatedAsync();

        // 2. Проверяем/Создаем системного пользователя (владелец глобальных чатов)
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
                Role = UserRole.Admin,
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

        // 3. Проверяем/Создаем глобальный публичный чат (ищем по ИМЕНИ, а не по ID)
        var globalChat = await context.ChatRooms
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Name == "Global Chat");
            
        if (globalChat == null)
        {
            globalChat = new ChatRoom
            {
                Name = "Global Chat",
                Description = "Official public chat for all users",
                Type = ChatRoomType.Public,
                CreatorId = systemUser.Id, // Владелец - система
                CreatedAt = DateTime.UtcNow,
                MaxMembers = 1000000 // Большой лимит для публичного чата
            };
            context.ChatRooms.Add(globalChat);
            await context.SaveChangesAsync();
            
            // Добавляем System бота как участника
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
