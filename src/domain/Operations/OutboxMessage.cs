namespace EnterpriseKnowledgeAgent.Domain.Operations;

/// <summary>
/// TASK-JOB-04: the transactional outbox row (Decision D1, ASDD §12.3). A
/// row is inserted in the SAME database transaction that creates the
/// job/run it announces (TASK-JOB-02); a background publisher later reads
/// undelivered rows and sends the corresponding SQS message, guaranteeing a
/// committed job always eventually gets a message even if SQS was briefly
/// unavailable at commit time.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public OutboxMessageType MessageType { get; private set; }

    /// <summary>JSON-serialized <c>IQueueMessage</c> payload matching <see cref="MessageType"/>.</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    public bool Delivered { get; private set; }

    public int DeliveryAttempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(OutboxMessageType messageType, string payloadJson, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        MessageType = messageType,
        PayloadJson = payloadJson,
        Delivered = false,
        DeliveryAttempts = 0,
        CreatedAt = now,
    };

    public void MarkDelivered(DateTimeOffset now)
    {
        Delivered = true;
        DeliveredAt = now;
        LastError = null;
    }

    /// <summary>Transient send failure - the row stays undelivered so the publisher (or the reconciler, TASK-JOB-05) retries later.</summary>
    public void RecordDeliveryFailure(string error, DateTimeOffset now)
    {
        DeliveryAttempts++;
        LastError = error;
    }
}
