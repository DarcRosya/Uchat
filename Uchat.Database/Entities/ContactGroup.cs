namespace Uchat.Database.Entities;

/// <summary>
/// Группа (папка) контактов пользователя
/// Позволяет организовать контакты по категориям (Семья, Работа и т.д.)
/// </summary>
public class ContactGroup
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Владелец группы (User.Id)
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// Название группы (например "Семья")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Цвет группы (опционально) - строковый код (#RRGGBB или имя)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Навигационное свойство на владельца
    /// </summary>
    public User Owner { get; set; } = null!;

    /// <summary>
    /// Контакты в этой группе
    /// </summary>
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
