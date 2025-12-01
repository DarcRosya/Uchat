using Database.Entities;

namespace Database.Services.Chat;

public sealed class ChatResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ChatRoom? ChatRoom { get; }

    private ChatResult(bool isSuccess, ChatRoom? chatRoom, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ChatRoom = chatRoom;
        ErrorMessage = errorMessage;
    }

    public static ChatResult Success(ChatRoom chatRoom)
        => new(true, chatRoom, null);

    public static ChatResult Failure(string errorMessage)
        => new(false, null, errorMessage);
}
