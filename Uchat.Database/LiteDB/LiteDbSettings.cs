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
}
