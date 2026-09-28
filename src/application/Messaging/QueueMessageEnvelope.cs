using EnterpriseKnowledgeAgent.Shared.Messaging;

namespace EnterpriseKnowledgeAgent.Application.Messaging;

/// <summary>
/// TASK-SQS-01: a single received message plus the receipt handle needed to
/// delete it or extend its visibility timeout (lease renewal, TASK-JOB-03).
/// </summary>
/// <param name="Message">The deserialized identifier-only payload.</param>
/// <param name="ReceiptHandle">Opaque handle for this specific delivery - required by <see cref="IMessageQueue.DeleteAsync"/>/<see cref="IMessageQueue.ExtendVisibilityAsync"/>.</param>
/// <param name="ApproximateReceiveCount">How many times this logical message has been delivered (SQS <c>ApproximateReceiveCount</c> attribute) - used for max-receive/DLQ reasoning and duplicate-delivery detection.</param>
public sealed record QueueMessageEnvelope<TMessage>(TMessage Message, string ReceiptHandle, int ApproximateReceiveCount)
    where TMessage : IQueueMessage;
