namespace Uchat.Database.Entities;

/// <summary>
/// Категория группового чата (например, "Работа", "Хобби", "Образование")
/// </summary>
public class ChatRoomCategory
{
    /// <summary>
    /// Уникальный идентификатор категории
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Название категории
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Иконка категории (эмодзи или путь к иконке)
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Навигационное свойство: все группы этой категории
    /// </summary>
    public ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();
}