using System;

namespace Uchat.Server.Services.Messaging;

/// <summary>
/// Represents the outcome of a coordinated messaging operation.
/// </summary>
public sealed class MessagingResult
{
    public bool Success { get; }
    public string? MessageId { get; }
    public DateTime? SentAt { get; }
    public bool NeedsReconciliation { get; }
    public string? FailureReason { get; }
    public List<string> ClearedReplyMessageIds { get; }
    public int? ResurrectedUserId { get; private set; }

    private MessagingResult(
        bool success, 
        string? messageId, 
        DateTime? sentAt, 
        bool needsReconciliation, 
        string? failureReason, 
        int? resurrectedUserId = null, 
        List<string>? clearedReplyMessageIds = null)
    {
        Success = success;
        MessageId = messageId;
        SentAt = sentAt;
        ResurrectedUserId = resurrectedUserId; 
        NeedsReconciliation = needsReconciliation;
        FailureReason = failureReason;
        ClearedReplyMessageIds = clearedReplyMessageIds ?? new List<string>();
    }

    public static MessagingResult SuccessResult(
        string messageId, 
        DateTime sentAt, 
        int? resurrectedUserId = null, 
        List<string>? clearedReplyMessageIds = null)
    {
        return new MessagingResult(
            success: true, 
            messageId: messageId, 
            sentAt: sentAt, 
            needsReconciliation: false, 
            failureReason: null, 
            resurrectedUserId: resurrectedUserId, 
            clearedReplyMessageIds: clearedReplyMessageIds);
    }

    public static MessagingResult Failure(string reason, bool needsReconciliation = false)
        => new MessagingResult(false, null, null, needsReconciliation, reason);
}
