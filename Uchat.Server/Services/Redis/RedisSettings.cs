namespace Uchat.Server.Services.Redis;

public sealed record RedisSettings
{
    public string? ConnectionString { get; init; }
    public string InstanceName { get; init; } = "uchat";
    public string ChannelPrefix { get; init; } = "cache";
    public int DefaultEntryTtlSeconds { get; init; } = 86_400;
}
