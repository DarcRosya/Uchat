using Uchat.Database.Entities;

namespace Uchat.Server.Services.Chat;

public enum ChatErrorType { None, NotFound, Forbidden, Validation }

public sealed class ChatResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ChatRoom? ChatRoom { get; }
    public ChatErrorType ErrorType { get; }
    public ChatRoom? Data => ChatRoom;

    private ChatResult(bool success, ChatRoom? room, string? error, ChatErrorType type)
    {
        IsSuccess = success;
        ChatRoom = room;
        ErrorMessage = error;
        ErrorType = type;
    }

    public static ChatResult Success(ChatRoom room) => new(true, room, null, ChatErrorType.None);
    public static ChatResult NotFound() => new(false, null, "Not Found", ChatErrorType.NotFound);
    public static ChatResult Forbidden() => new(false, null, "Forbidden", ChatErrorType.Forbidden);
    public static ChatResult Failure(string error) => new(false, null, error, ChatErrorType.Validation);
}