using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace Uchat.Database.MongoDB;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;
    
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        
        InitializeIndexes();
    }
    
    public MongoDbContext(MongoDbSettings settings)
    {
        _settings = settings;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
        
        InitializeIndexes();
    }
    
    public IMongoCollection<MongoMessage> Messages => 
        _database.GetCollection<MongoMessage>(_settings.MessagesCollectionName);
    
    private void InitializeIndexes()
    {
        CreateMessagesIndexes();
    }
    
    private void CreateMessagesIndexes()
    {
        var messagesCollection = Messages;
        
        var chatIdSentAtIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId)
            .Descending(m => m.SentAt);
        
        var chatIdSentAtOptions = new CreateIndexOptions 
        { 
            Name = "idx_chatId_sentAt" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(chatIdSentAtIndex, chatIdSentAtOptions));
        }
        catch (MongoCommandException)
        {
            // index already exists, we ignoring so...
        }
        
        // To search for all messages in the chat (without filtering by time) 
        var chatIdIndex = Builders<MongoMessage>.IndexKeys
            .Ascending(m => m.ChatId);
        
        var chatIdOptions = new CreateIndexOptions 
        { 
            Name = "idx_chatId" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(chatIdIndex, chatIdOptions));
        }
        catch (MongoCommandException)
        {
            // index already exists
        }
        
        // To sort by time (global search)
        var sentAtIndex = Builders<MongoMessage>.IndexKeys
            .Descending(m => m.SentAt);
        
        var sentAtOptions = new CreateIndexOptions 
        { 
            Name = "idx_sentAt" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(sentAtIndex, sentAtOptions));
        }
        catch (MongoCommandException)
        {
            // index already exists
        }
        

        // To search for all messages from a user
        var senderUserIdIndex = Builders<MongoMessage>.IndexKeys
            .Ascending("sender.userId");
        
        var senderUserIdOptions = new CreateIndexOptions 
        { 
            Name = "idx_sender_userId" 
        };
        
        try
        {
            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<MongoMessage>(senderUserIdIndex, senderUserIdOptions));
        }
        catch (MongoCommandException)
        {
            // index already exists
        }
    }
}
