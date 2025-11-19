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
        // Создаем билдер опций
        var optionsBuilder = new DbContextOptionsBuilder<UchatDbContext>();
        
        // Настраиваем SQLite с путем к файлу БД
        // В реальном проекте путь берется из appsettings.json
        // Здесь используем относительный путь для разработки
        optionsBuilder.UseSqlite("Data Source=uchat.db");
        
        // Опционально: можно добавить логирование для отладки
        // optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        
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
 * 3. Путь "Data Source=uchat.db" создаст файл в папке проекта
 * 4. Можно изменить путь, например:
 *    - "Data Source=../uchat.db" (на уровень выше)
 *    - "Data Source=C:/Databases/uchat.db" (абсолютный путь)
 * 
 * ============================================================================
 */
