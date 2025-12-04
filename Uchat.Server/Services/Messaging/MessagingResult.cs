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

    private MessagingResult(bool success, string? messageId, DateTime? sentAt, bool needsReconciliation, string? failureReason, List<string>? clearedReplyMessageIds = null)
    {
        Success = success;
        MessageId = messageId;
        SentAt = sentAt;
        NeedsReconciliation = needsReconciliation;
        FailureReason = failureReason;
        ClearedReplyMessageIds = clearedReplyMessageIds ?? new List<string>();
    }

    public static MessagingResult SuccessResult(string messageId, DateTime sentAt, List<string>? clearedReplyMessageIds = null)
        => new(true, messageId, sentAt, needsReconciliation: false, failureReason: null, clearedReplyMessageIds);

    public static MessagingResult Failure(string reason, bool needsReconciliation = false)
        => new(false, messageId: null, sentAt: null, needsReconciliation, reason);
}
