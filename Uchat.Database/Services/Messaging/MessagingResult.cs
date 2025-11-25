using System;

namespace Uchat.Database.Services.Messaging;

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

    private MessagingResult(bool success, string? messageId, DateTime? sentAt, bool needsReconciliation, string? failureReason)
    {
        Success = success;
        MessageId = messageId;
        SentAt = sentAt;
        NeedsReconciliation = needsReconciliation;
        FailureReason = failureReason;
    }

    public static MessagingResult SuccessResult(string messageId, DateTime sentAt)
        => new(true, messageId, sentAt, needsReconciliation: false, failureReason: null);

    public static MessagingResult Failure(string reason, bool needsReconciliation = false)
        => new(false, messageId: null, sentAt: null, needsReconciliation, reason);
}
