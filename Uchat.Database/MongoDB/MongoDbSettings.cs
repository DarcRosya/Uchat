/*
 * ============================================================================
 * MONGODB SETTINGS (Настройки подключения к MongoDB)
 * ============================================================================
 * 
 * Этот класс хранит настройки подключения к MongoDB.
 * Загружается из appsettings.json через IOptions<MongoDbSettings>
 * 
 * ============================================================================
 */

namespace Uchat.Database.MongoDB;

/// <summary>
/// Настройки подключения к MongoDB
/// 
/// Загружается из appsettings.json:
/// {
///   "MongoDb": {
///     "ConnectionString": "mongodb+srv://user:pass@cluster.mongodb.net/",
///     "DatabaseName": "uchat-dev",
///     "MessagesCollectionName": "messages"
///   }
/// }
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Строка подключения к MongoDB
    /// Примеры:
    /// - "mongodb://localhost:27017" (локальный)
    /// - "mongodb+srv://cluster.mongodb.net/" (MongoDB Atlas)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя базы данных
    /// Пример: "uchat-dev", "uchat-production"
    /// </summary>
    public string DatabaseName { get; set; } = "uchat-dev";
    
    /// <summary>
    /// Имя коллекции для хранения сообщений
    /// По умолчанию: "messages"
    /// </summary>
    public string MessagesCollectionName { get; set; } = "messages";
}
