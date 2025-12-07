namespace Uchat.Database.MongoDB;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "uchat";
    public string MessagesCollectionName { get; set; } = "messages";
}
