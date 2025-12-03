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
///   "MongoDB": {
///     "ConnectionString": "mongodb+srv://user:password@cluster.mongodb.net/",
///     "DatabaseName": "uchat",
///     "MessagesCollectionName": "messages"
///   }
/// }
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Connection string для MongoDB Atlas или локального сервера
    /// Примеры:
    /// - "mongodb://localhost:27017" (локальный)
    /// - "mongodb+srv://user:password@cluster.mongodb.net/" (Atlas)
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    
    /// <summary>
    /// Имя базы данных MongoDB
    /// По умолчанию: "uchat"
    /// </summary>
    public string DatabaseName { get; set; } = "uchat";
    
    /// <summary>
    /// Имя коллекции для хранения сообщений
    /// По умолчанию: "messages"
    /// </summary>
    public string MessagesCollectionName { get; set; } = "messages";
}
