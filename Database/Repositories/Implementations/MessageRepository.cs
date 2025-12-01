/*
 * ============================================================================
 * REPOSITORY IMPLEMENTATION: Message Repository
 * ============================================================================
 * 
 * Реализация IMessageRepository
 * 
 * Предоставляет методы для работы с сообщениями в LiteDB
 * 
 * ============================================================================
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Repositories;

/// <summary>
/// Репозиторий для работы с сообщениями в LiteDB
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly LiteDbContext _context;
    private readonly ILiteCollection<LiteDbMessage> _messages;
    private readonly ILiteDbWriteGate _writeGate;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public MessageRepository(LiteDbContext context, ILiteDbWriteGate writeGate)
    {
        _context = context;
        _messages = context.Messages;
        _writeGate = writeGate;
    }
    
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ (READ OPERATIONS)
    // ========================================================================
    // 
    // ⚠️ ВАЖНО:
    // - READ операции: можно использовать напрямую
    // - UPDATE/DELETE операции: требуют проверки прав в вызывающем коде
    // - CREATE операции: ТОЛЬКО через MessageService (валидация + координация 2 БД)
    
    // ========================================================================
    // ПОЛУЧЕНИЕ СООБЩЕНИЙ
    // ========================================================================
    
    /// <summary>
    /// Получить сообщения чата (CURSOR-BASED PAGINATION)
    /// 
    /// Использует составной индекс (ChatId, SentAt DESC) для мгновенной загрузки
    /// 
    /// Параметры:
    /// - chatId: ID чата
    /// - limit: количество сообщений (по умолчанию 50)
    /// - lastTimestamp: время последнего сообщения (для пагинации)
    ///   
    ///   null = первая загрузка (последние 50 сообщений)
    ///   DateTime = загрузить старые сообщения до этого времени
    /// 
    /// Примеры:
    /// 
    ///   // Первая загрузка (последние 50 сообщений)
    ///   var messages = await GetChatMessagesAsync(chatId: 1, limit: 50);
    ///   var lastTimestamp = messages.Last().SentAt;
    ///   
    ///   // Загрузить еще 50 (старые сообщения)
    ///   var olderMessages = await GetChatMessagesAsync(chatId: 1, limit: 50, lastTimestamp: lastTimestamp);
    /// 
    /// Преимущества CURSOR-BASED над OFFSET-BASED:
    /// ✅ Мгновенный поиск по индексу (O(log n))
    /// ✅ Стабильные результаты (новые сообщения не влияют)
    /// ✅ Поддержка бесконечной прокрутки
    /// 
    /// ❌ OFFSET-BASED (старый способ):
    ///   - Медленно на больших offset (сканирует все пропущенные строки)
    ///   - Пропускает новые сообщения (непредсказуемо)
    /// </summary>
    public async Task<List<LiteDbMessage>> GetChatMessagesAsync(int chatId, int limit = 50, DateTime? lastTimestamp = null)
    {
        IEnumerable<LiteDbMessage> query;
        
        if (lastTimestamp == null)
        {
            // ПЕРВАЯ ЗАГРУЗКА: последние N сообщений
            // Использует составной индекс (ChatId, SentAt DESC)
            query = _messages
                .Find(m => m.ChatId == chatId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt);
        }
        else
        {
            // ЗАГРУЗИТЬ ЕЩЕ: старые сообщения до lastTimestamp
            // Использует составной индекс (ChatId, SentAt DESC)
            // WHERE chatId = X AND sentAt < lastTimestamp
            query = _messages
                .Find(m => m.ChatId == chatId && !m.IsDeleted && m.SentAt < lastTimestamp.Value)
                .OrderByDescending(m => m.SentAt);
        }
        
        var result = query
            .Take(limit)
            .ToList();
            
        return await Task.FromResult(result);
    }
    
    public async Task<LiteDbMessage?> GetMessageByIdAsync(string messageId)
    {
        var result = _messages
            .FindById(messageId);
            
        return await Task.FromResult(result);
    }
    
    public async Task<List<LiteDbMessage>> GetUnreadMessagesAsync(int chatId, int userId)
    {
        // LiteDB запрос: найти сообщения где userId НЕ в массиве readBy
        var result = _messages
            .Find(m => m.ChatId == chatId && !m.IsDeleted && !m.ReadBy.Contains(userId))
            .OrderByDescending(m => m.SentAt)
            .ToList();
            
        return await Task.FromResult(result);
    }
    
    public async Task<long> GetUnreadCountAsync(int chatId, int userId)
    {
        var count = _messages.Count(m => m.ChatId == chatId && !m.IsDeleted && !m.ReadBy.Contains(userId));
        
        return await Task.FromResult(count);
    }
    
    // ========================================================================
    // РЕДАКТИРОВАНИЕ И УДАЛЕНИЕ
    // ========================================================================
    
    public async Task<bool> EditMessageAsync(string messageId, string newContent)
    {
        using var gate = await _writeGate.AcquireAsync();

        var message = _messages.FindById(messageId);
        if (message == null)
        {
            return false;
        }

        message.Content = newContent;
        message.EditedAt = DateTime.UtcNow;

        var result = _messages.Update(message);

        return await Task.FromResult(result);
    }
    
    public async Task<bool> DeleteMessageAsync(string messageId)
    {
        using var gate = await _writeGate.AcquireAsync();

        var message = _messages.FindById(messageId);
        if (message == null)
        {
            return false;
        }

        message.IsDeleted = true;

        var result = _messages.Update(message);

        return await Task.FromResult(result);
    }
    
    // ========================================================================
    // РЕАКЦИИ
    // ========================================================================
    
    public async Task<bool> AddReactionAsync(string messageId, string emoji, int userId)
    {
        using var gate = await _writeGate.AcquireAsync();

        var message = _messages.FindById(messageId);
        if (message == null)
        {
            return false;
        }

        if (!message.Reactions.ContainsKey(emoji))
        {
            message.Reactions[emoji] = new List<int>();
        }

        if (!message.Reactions[emoji].Contains(userId))
        {
            message.Reactions[emoji].Add(userId);
        }

        var result = _messages.Update(message);

        return await Task.FromResult(result);
    }
    
    public async Task<bool> RemoveReactionAsync(string messageId, string emoji, int userId)
    {
        using var gate = await _writeGate.AcquireAsync();

        var message = _messages.FindById(messageId);
        if (message == null)
        {
            return false;
        }

        if (message.Reactions.ContainsKey(emoji))
        {
            message.Reactions[emoji].Remove(userId);

            if (message.Reactions[emoji].Count == 0)
            {
                message.Reactions.Remove(emoji);
            }
        }

        var result = _messages.Update(message);

        return await Task.FromResult(result);
    }
    
    // ========================================================================
    // СТАТУС ПРОЧТЕНИЯ
    // ========================================================================
    
    public async Task<bool> MarkAsReadAsync(string messageId, int userId)
    {
        using var gate = await _writeGate.AcquireAsync();

        var message = _messages.FindById(messageId);
        if (message == null)
        {
            return false;
        }

        if (!message.ReadBy.Contains(userId))
        {
            message.ReadBy.Add(userId);
        }

        var result = _messages.Update(message);

        return await Task.FromResult(result);
    }
    
    public async Task<long> MarkAllAsReadAsync(int chatId, int userId)
    {
        using var gate = await _writeGate.AcquireAsync();

        var unreadMessages = _messages
            .Find(m => m.ChatId == chatId && !m.IsDeleted && !m.ReadBy.Contains(userId))
            .ToList();

        long count = 0;
        foreach (var message in unreadMessages)
        {
            message.ReadBy.Add(userId);
            if (_messages.Update(message))
            {
                count++;
            }
        }

        return await Task.FromResult(count);
    }
    
    public async Task<long> MarkAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp)
    {
        using var gate = await _writeGate.AcquireAsync();

        // Находим все непрочитанные сообщения до указанного времени
        var unreadMessages = _messages
            .Find(m => m.ChatId == chatId 
                    && !m.IsDeleted 
                    && m.SentAt <= untilTimestamp 
                    && !m.ReadBy.Contains(userId))
            .ToList();

        long count = 0;
        foreach (var message in unreadMessages)
        {
            message.ReadBy.Add(userId);
            if (_messages.Update(message))
            {
                count++;
            }
        }

        return await Task.FromResult(count);
    }
    
    // ========================================================================
    // ПОИСК
    // ========================================================================
    
    public async Task<List<LiteDbMessage>> SearchMessagesAsync(int chatId, string searchQuery, int limit = 20)
    {
        // Фильтр: поиск по тексту (case-insensitive)
        var result = _messages
            .Find(m => m.ChatId == chatId && !m.IsDeleted && m.Content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToList();
            
        return await Task.FromResult(result);
    }
}

/*
 * ============================================================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ============================================================================
 * 
 * 1. ОТПРАВКА СООБЩЕНИЯ:
 * 
 *    var repo = new MessageRepository(liteDbContext);
 *    
 *    var message = new LiteDbMessage
 *    {
 *        ChatId = 1,
 *        Sender = new MessageSender
 *        {
 *            UserId = 100,
 *            Username = "alice",
 *            DisplayName = "Alice Smith",
 *            AvatarUrl = "/alice.jpg"
 *        },
 *        Content = "Hello everyone!",
 *        Type = "text"
 *    };
 *    
 *    var messageId = await repo.SendMessageAsync(message);
 *    Console.WriteLine($"Сообщение отправлено: {messageId}");
 * 
 * 
 * 2. CURSOR-BASED PAGINATION (загрузка истории чата):
 * 
 *    // ПЕРВАЯ ЗАГРУЗКА: последние 50 сообщений
 *    var messages = await repo.GetChatMessagesAsync(chatId: 1, limit: 50);
 *    
 *    // Запоминаем timestamp последнего сообщения
 *    DateTime? lastTimestamp = messages.LastOrDefault()?.SentAt;
 *    
 *    // ЗАГРУЗИТЬ ЕЩЕ: следующие 50 старых сообщений
 *    if (lastTimestamp != null)
 *    {
 *        var olderMessages = await repo.GetChatMessagesAsync(
 *            chatId: 1, 
 *            limit: 50, 
 *            lastTimestamp: lastTimestamp
 *        );
 *    }
 *    
 *    // Преимущества:
 *    // ✅ Мгновенная загрузка (использует составной индекс)
 *    // ✅ Стабильные результаты (новые сообщения не влияют)
 *    // ✅ Бесконечная прокрутка (нет ограничения по OFFSET)
 * 
 * 
 * 2. ДОБАВЛЕНИЕ РЕАКЦИИ:
 * 
 *    await repo.AddReactionAsync(messageId, "👍", userId: 100);
 *    await repo.AddReactionAsync(messageId, "❤️", userId: 200);
 * 
 * 
 * 3. ПОМЕТИТЬ КАК ПРОЧИТАННОЕ:
 * 
 *    // Одно сообщение
 *    await repo.MarkAsReadAsync(messageId, userId: 100);
 *    
 *    // Все сообщения в чате
 *    var count = await repo.MarkAllAsReadAsync(chatId: 1, userId: 100);
 *    Console.WriteLine($"Помечено {count} сообщений");
 * 
 * 
 * 4. РЕДАКТИРОВАНИЕ (⚠️ с проверкой прав!):
 * 
 *    // Проверка прав должна быть в Controller:
 *    var message = await repo.GetMessageByIdAsync(messageId);
 *    if (message.Sender.UserId == currentUserId)
 *        await repo.EditMessageAsync(messageId, "Updated message text");
 * 
 * 
 * 5. УДАЛЕНИЕ (⚠️ с проверкой прав!):
 * 
 *    // Проверка прав должна быть в Controller:
 *    var message = await repo.GetMessageByIdAsync(messageId);
 *    if (message.Sender.UserId == currentUserId || isAdmin)
 *        await repo.DeleteMessageAsync(messageId);
 *    // Сообщение скрыто (isDeleted = true)
 * 
 * 
 * 6. ПОИСК:
 * 
 *    var results = await repo.SearchMessagesAsync(chatId: 1, "hello");
 *    Console.WriteLine($"Найдено {results.Count} сообщений");
 * 
 * 
 * 8. ПОЛУЧЕНИЕ НЕПРОЧИТАННЫХ:
 * 
 *    var unread = await repo.GetUnreadMessagesAsync(chatId: 1, userId: 100);
 *    var unreadCount = await repo.GetUnreadCountAsync(chatId: 1, userId: 100);
 *    
 *    Console.WriteLine($"Непрочитанных: {unreadCount}");
 * 
 * ============================================================================
 * 9. CURSOR-BASED PAGINATION В WPF (C#)
 * ============================================================================
 * 
 * // ViewModel для чата с поддержкой бесконечной прокрутки
 * public class ChatViewModel : INotifyPropertyChanged
 * {
 *     private readonly IMessageRepository _messageRepository;
 *     private int _currentChatId;
 *     private DateTime? _lastTimestamp;
 *     private bool _isLoading;
 *     private bool _hasMoreMessages = true;
 *     
 *     public ObservableCollection<LiteDbMessage> Messages { get; } = new();
 *     
 *     public bool IsLoading
 *     {
 *         get => _isLoading;
 *         set { _isLoading = value; OnPropertyChanged(); }
 *     }
 *     
 *     public ChatViewModel(IMessageRepository messageRepository)
 *     {
 *         _messageRepository = messageRepository;
 *     }
 *     
 *     // ПЕРВАЯ ЗАГРУЗКА: при открытии чата
 *     public async Task LoadMessagesAsync(int chatId)
 *     {
 *         _currentChatId = chatId;
 *         _lastTimestamp = null;
 *         _hasMoreMessages = true;
 *         Messages.Clear();
 *         
 *         IsLoading = true;
 *         try
 *         {
 *             var messages = await _messageRepository.GetChatMessagesAsync(chatId, limit: 50);
 *             
 *             // Добавляем в ObservableCollection (UI обновится автоматически)
 *             foreach (var message in messages)
 *             {
 *                 Messages.Add(message);
 *             }
 *             
 *             // Запоминаем timestamp последнего сообщения
 *             if (messages.Count > 0)
 *             {
 *                 _lastTimestamp = messages[^1].SentAt; // C# 8.0+ синтаксис
 *             }
 *             
 *             _hasMoreMessages = messages.Count == 50; // Если вернулось меньше 50, больше нет
 *         }
 *         finally
 *         {
 *             IsLoading = false;
 *         }
 *     }
 *     
 *     // ЗАГРУЗИТЬ ЕЩЕ: при скролле вверх (к старым сообщениям)
 *     public async Task LoadMoreMessagesAsync()
 *     {
 *         if (!_hasMoreMessages || IsLoading || _lastTimestamp == null)
 *             return;
 *         
 *         IsLoading = true;
 *         try
 *         {
 *             var olderMessages = await _messageRepository.GetChatMessagesAsync(
 *                 _currentChatId, 
 *                 limit: 50, 
 *                 lastTimestamp: _lastTimestamp
 *             );
 *             
 *             // Добавляем старые сообщения в конец списка
 *             foreach (var message in olderMessages)
 *             {
 *                 Messages.Add(message);
 *             }
 *             
 *             // Обновляем курсор
 *             if (olderMessages.Count > 0)
 *             {
 *                 _lastTimestamp = olderMessages[^1].SentAt;
 *             }
 *             
 *             _hasMoreMessages = olderMessages.Count == 50;
 *         }
 *         finally
 *         {
 *             IsLoading = false;
 *         }
 *     }
 *     
 *     public event PropertyChangedEventHandler? PropertyChanged;
 *     protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
 *     {
 *         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
 *     }
 * }
 * 
 * 
 * // XAML: ScrollViewer с обработчиком скролла
 * // <ScrollViewer x:Name="MessageScrollViewer" 
 * //               ScrollChanged="MessageScrollViewer_OnScrollChanged">
 * //     <ItemsControl ItemsSource="{Binding Messages}">
 * //         <!-- Шаблон сообщения -->
 * //     </ItemsControl>
 * // </ScrollViewer>
 * 
 * 
 * // Code-behind: определение момента загрузки старых сообщений
 * private async void MessageScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
 * {
 *     var scrollViewer = (ScrollViewer)sender;
 *     
 *     // Проверяем, достиг ли пользователь верхней части списка (старые сообщения)
 *     if (scrollViewer.VerticalOffset < 100) // Порог 100 пикселей от верха
 *     {
 *         var viewModel = (ChatViewModel)DataContext;
 *         await viewModel.LoadMoreMessagesAsync();
 *     }
 * }
 * 
 * 
 * // АЛЬТЕРНАТИВА: RelayCommand для кнопки "Загрузить еще"
 * public class ChatViewModel : INotifyPropertyChanged
 * {
 *     public ICommand LoadMoreCommand { get; }
 *     
 *     public ChatViewModel(IMessageRepository messageRepository)
 *     {
 *         _messageRepository = messageRepository;
 *         
 *         LoadMoreCommand = new RelayCommand(
 *             execute: async () => await LoadMoreMessagesAsync(),
 *             canExecute: () => _hasMoreMessages && !IsLoading
 *         );
 *     }
 * }
 * 
 * // XAML:
 * // <Button Content="Загрузить старые сообщения" 
 * //         Command="{Binding LoadMoreCommand}"
 * //         Visibility="{Binding HasMoreMessages, Converter={StaticResource BoolToVisibilityConverter}}"/>
 * 
 * ============================================================================
 */
