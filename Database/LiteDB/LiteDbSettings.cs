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

namespace Database.LiteDB;

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
    /// Шаблон имени файла, если включено шардирование (например "messages-{chatId}.db").
    /// Используйте {chatId}, {date} и другие шаблоны.
    /// </summary>
    public string MessagesCollectionName { get; set; } = "messages";
    public string DatabasePath { get; set; } = "messages.db";

    /// - "Manual" - бэкапы создаются только вручную через API
    /// - "Automatic" - автоматические бэкапы по расписанию
    public string BackupMode { get; set; } = "Manual";
    public string BackupDirectory { get; set; } = "Backups";

    /// <summary>
    /// Количество свежих бэкапов, которые нужно хранить
    /// По умолчанию: 7
    /// </summary>
    public int BackupRetention { get; set; } = 7;

    /// <summary>
    /// Интервал между автоматическими бэкапами в минутах (только для Automatic режима)
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
