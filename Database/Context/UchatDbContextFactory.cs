/*
 * ============================================================================
 * DESIGN-TIME DBCONTEXT FACTORY
 * ============================================================================
 * 
 * Этот класс нужен только для Entity Framework Tools (dotnet ef migrations)
 * 
 * ЗАЧЕМ?
 * - dotnet ef migrations add нужно создать экземпляр DbContext
 * - Но проект Uchat.Database - это библиотека, не приложение
 * - Нет Program.cs, нет Dependency Injection
 * - Поэтому EF Tools не знает, как создать UchatDbContext
 * 
 * РЕШЕНИЕ:
 * - Создаем IDesignTimeDbContextFactory
 * - EF Tools автоматически найдут этот класс
 * - Будут использовать его для создания DbContext при миграциях
 * 
 * ============================================================================
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Uchat.Database.Context;

/// <summary>
/// Фабрика для создания DbContext во время разработки (миграции)
/// 
/// EF Tools автоматически находит этот класс и использует для:
/// - dotnet ef migrations add
/// - dotnet ef database update
/// - dotnet ef migrations remove
/// </summary>
public class UchatDbContextFactory : IDesignTimeDbContextFactory<UchatDbContext>
{
    /// <summary>
    /// Создает экземпляр UchatDbContext для инструментов миграций
    /// </summary>
    /// <param name="args">Аргументы командной строки (обычно пустые)</param>
    /// <returns>Настроенный DbContext</returns>
    public UchatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UchatDbContext>();
        
        // Попытка получить connection string из переменной окружения
        var connectionString = Environment.GetEnvironmentVariable("UCHAT_CONNECTION_STRING");
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            // По умолчанию используем PostgreSQL для локальной сети
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=uchat;Username=uchat;Password=uchat123");
        }
        
        return new UchatDbContext(optionsBuilder.Options);
    }
}

/*
 * ============================================================================
 * ПРИМЕЧАНИЯ:
 * ============================================================================
 * 
 * 1. Этот класс используется ТОЛЬКО во время разработки
 * 2. В реальном приложении DbContext настраивается в Program.cs
 * 3. Connection string по умолчанию для локального PostgreSQL
 * 4. Можно изменить через переменную окружения UCHAT_CONNECTION_STRING
 * 5. Для Railway используй переменную окружения DATABASE_URL
 * 
 * ============================================================================
 */
