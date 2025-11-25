/*
 * ============================================================================
 * LITEDB SETTINGS (Настройки подключения к LiteDB)
 * ============================================================================
 * 
 * Этот класс хранит настройки подключения к LiteDB.
 * Загружается из appsettings.json через IOptions<LiteDbSettings>
 * 
 * ============================================================================
 */

namespace Uchat.Database.LiteDB;

/// <summary>
/// Настройки подключения к LiteDB
/// 
/// Загружается из appsettings.json:
/// {
///   "LiteDb": {
///     "DatabasePath": "Data/messages.db",
///     "MessagesCollectionName": "messages"
///   }
/// }
/// </summary>
public class LiteDbSettings
{
    /// <summary>
    /// Путь к файлу базы данных LiteDB
    /// Примеры:
    /// - "messages.db" (в корне приложения)
    /// - "Data/messages.db" (в папке Data)
    /// - "C:/Databases/messages.db" (абсолютный путь)
    /// </summary>
    public string DatabasePath { get; set; } = "messages.db";
    
    /// <summary>
    /// Имя коллекции для хранения сообщений
    /// По умолчанию: "messages"
    /// </summary>
    public string MessagesCollectionName { get; set; } = "messages";

    /// <summary>
    /// Максимальный возраст сообщений до удаления
    /// По умолчанию 30 дней
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Интервал между проверками очистки в минутах
    /// По умолчанию раз в час
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Папка для резервных копий LiteDB
    /// По умолчанию: "Backups"
    /// </summary>
    public string BackupDirectory { get; set; } = "Backups";

    /// <summary>
    /// Количество свежих бэкапов, которые нужно хранить
    /// По умолчанию: 7
    /// </summary>
    public int BackupRetention { get; set; } = 7;

    /// <summary>
    /// Интервал между полными бэкапами в минутах
    /// По умолчанию: 1440 (раз в сутки)
    /// </summary>
    public int BackupIntervalMinutes { get; set; } = 1440;

    /// <summary>
    /// Включить шардирование файлов по chatId
    /// По умолчанию: false
    /// </summary>
    public bool EnableSharding { get; set; } = false;

    /// <summary>
    /// Шаблон имени файла, если включено шардирование (например "messages-{chatId}.db").
    /// Используйте {chatId}, {date} и другие шаблоны.
    /// </summary>
    public string ShardFilePattern { get; set; } = "messages.db";
}
