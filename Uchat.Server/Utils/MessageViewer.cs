using LiteDB;
using Uchat.Database.LiteDB;

namespace Uchat.Server.Utils;

/// <summary>
/// Утилита для просмотра сообщений в LiteDB
/// Запустите из терминала: dotnet run --project MessageViewer.csproj
/// </summary>
public class MessageViewer
{
    //public static void Main(string[] args)
    //{
    //    var dbPath = "Data/messages.db";
        
    //    if (!File.Exists(dbPath))
    //    {
    //        Console.WriteLine($"❌ База данных не найдена: {dbPath}");
    //        Console.WriteLine("Отправьте хотя бы одно сообщение через чат!");
    //        return;
    //    }

    //    using var db = new LiteDatabase(dbPath);
    //    var messages = db.GetCollection<LiteDbMessage>("messages");
        
    //    var allMessages = messages.FindAll().OrderBy(m => m.SentAt).ToList();
        
    //    Console.WriteLine($"📊 Всего сообщений в базе: {allMessages.Count}\n");
    //    Console.WriteLine("=" + new string('=', 80));
        
    //    foreach (var msg in allMessages)
    //    {
    //        Console.WriteLine($"\n💬 ID: {msg.Id}");
    //        Console.WriteLine($"   Chat: {msg.ChatId}");
    //        Console.WriteLine($"   From: {msg.Sender.Username} (ID: {msg.Sender.UserId})");
    //        Console.WriteLine($"   Text: {msg.Content}");
    //        Console.WriteLine($"   Time: {msg.SentAt:yyyy-MM-dd HH:mm:ss}");
    //        Console.WriteLine($"   Type: {msg.Type}");
    //        if (msg.EditedAt.HasValue)
    //            Console.WriteLine($"   ✏️ Edited: {msg.EditedAt:yyyy-MM-dd HH:mm:ss}");
    //        if (msg.IsDeleted)
    //            Console.WriteLine($"   🗑️ Deleted: true");
    //    }
        
    //    Console.WriteLine("\n" + new string('=', 80));
        
    //    // Группировка по чатам
    //    var byChat = allMessages.GroupBy(m => m.ChatId);
    //    Console.WriteLine($"\n📁 Сообщений по чатам:");
    //    foreach (var group in byChat)
    //    {
    //        Console.WriteLine($"   Chat #{group.Key}: {group.Count()} сообщений");
    //    }
        
    //    // Последнее сообщение
    //    var lastMsg = allMessages.LastOrDefault();
    //    if (lastMsg != null)
    //    {
    //        Console.WriteLine($"\n🕐 Последнее сообщение:");
    //        Console.WriteLine($"   {lastMsg.Sender.Username}: {lastMsg.Content}");
    //        Console.WriteLine($"   Отправлено: {lastMsg.SentAt:yyyy-MM-dd HH:mm:ss}");
    //    }
    //}
}
