using System.Text;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.Chat;
using EnterpriseKnowledgeAgent.Domain.Conversations;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// Module CHAT: the ChatGPT-style knowledge-base assistant API, all scoped to
/// <c>/api/applications/{applicationId}</c>. Open to any caller - there is no
/// login/authorization in this app. Answers stream back token-by-token over
/// Server-Sent Events (see <see cref="ChatCompletionService"/>); every other
/// endpoint returns ordinary JSON.
/// </summary>
public static class ChatEndpoints
{
    private const string Actor = "local-user";
    private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications/{applicationId:guid}/conversations")
            .WithTags("Chat");

        // --- Start a new conversation ---
        group.MapPost("/", async (
            Guid applicationId,
            CreateConversationRequest? request,
            ConversationService conversationService,
            IApplicationRepository applicationRepository,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                var detail = await conversationService.CreateAsync(
                    applicationId, Actor, request ?? new CreateConversationRequest(), cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.ConversationCreated, Actor, applicationId: applicationId,
                    entityType: "Conversation", entityId: detail.Id.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Created(
                    $"/api/applications/{applicationId}/conversations/{detail.Id}", detail);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    title: "Invalid conversation request",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // --- List conversations (shared across the application) ---
        group.MapGet("/", async (
            Guid applicationId,
            ConversationService conversationService,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            var conversations = await conversationService.ListAsync(applicationId, cancellationToken);
            return Results.Ok(conversations);
        });

        // --- Conversation detail (full message history) ---
        group.MapGet("/{conversationId:guid}", async (
            Guid applicationId,
            Guid conversationId,
            ConversationService conversationService,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                var detail = await conversationService.GetDetailAsync(applicationId, conversationId, cancellationToken);
                return Results.Ok(detail);
            }
            catch (ConversationNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // --- Rename ---
        group.MapPatch("/{conversationId:guid}", async (
            Guid applicationId,
            Guid conversationId,
            RenameConversationRequest request,
            ConversationService conversationService,
            IConversationRepository conversationRepository,
            IApplicationRepository applicationRepository,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var conversation = await GetConversationOrNotFoundAsync(
                applicationId, conversationId, applicationRepository, conversationRepository, cancellationToken);
            if (conversation.Result is not null)
            {
                return conversation.Result;
            }

            try
            {
                var summary = await conversationService.RenameAsync(conversation.Conversation!, request.Title, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.ConversationRenamed, Actor, applicationId: applicationId,
                    entityType: "Conversation", entityId: conversationId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Ok(summary);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    title: "Invalid conversation title",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // --- Soft-delete ---
        group.MapDelete("/{conversationId:guid}", async (
            Guid applicationId,
            Guid conversationId,
            ConversationService conversationService,
            IConversationRepository conversationRepository,
            IApplicationRepository applicationRepository,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var conversation = await GetConversationOrNotFoundAsync(
                applicationId, conversationId, applicationRepository, conversationRepository, cancellationToken);
            if (conversation.Result is not null)
            {
                return conversation.Result;
            }

            await conversationService.DeleteAsync(conversation.Conversation!, cancellationToken);
            await auditWriter.WriteAsync(
                AuditActions.ConversationDeleted, Actor, applicationId: applicationId,
                entityType: "Conversation", entityId: conversationId.ToString(),
                cancellationToken: cancellationToken);
            return Results.NoContent();
        });

        // --- Send a message; the answer streams back over Server-Sent Events ---
        group.MapPost("/{conversationId:guid}/messages", async (
            Guid applicationId,
            Guid conversationId,
            SendMessageRequest request,
            ChatCompletionService chatCompletionService,
            IConversationRepository conversationRepository,
            IApplicationRepository applicationRepository,
            IAuditWriter auditWriter,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.Problem(
                    title: "Invalid message",
                    detail: "Message content is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (request.Content.Length > ConversationMessage.MaxUserContentLength)
            {
                return Results.Problem(
                    title: "Message too long",
                    detail: $"Message content must not exceed {ConversationMessage.MaxUserContentLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var conversation = await conversationRepository.GetAsync(applicationId, conversationId, cancellationToken);
            if (conversation is null)
            {
                return Results.NotFound();
            }

            await auditWriter.WriteAsync(
                AuditActions.ChatMessageSent, Actor, applicationId: applicationId,
                entityType: "Conversation", entityId: conversationId.ToString(),
                cancellationToken: cancellationToken);

            var response = httpContext.Response;
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            // Disable proxy (nginx) response buffering so tokens flush to the
            // browser immediately rather than being held back until the
            // response completes.
            response.Headers["X-Accel-Buffering"] = "no";
            await response.Body.FlushAsync(cancellationToken);

            try
            {
                await foreach (var streamEvent in chatCompletionService
                    .StreamAnswerAsync(conversation, request.Content, cancellationToken))
                {
                    await WriteSseEventAsync(response, streamEvent, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The client disconnected mid-stream; ChatCompletionService
                // finalizes the in-flight assistant message as Cancelled in
                // its own finally block, so nothing is left dangling here.
            }

            return Results.Empty;
        });

        return endpoints;
    }

    /// <summary>Loads the application and conversation, returning a short-circuit 404 <see cref="IResult"/> if either is missing.</summary>
    private static async Task<(IResult? Result, Conversation? Conversation)> GetConversationOrNotFoundAsync(
        Guid applicationId,
        Guid conversationId,
        IApplicationRepository applicationRepository,
        IConversationRepository conversationRepository,
        CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return (Results.NotFound(), null);
        }

        var conversation = await conversationRepository.GetAsync(applicationId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return (Results.NotFound(), null);
        }

        return (null, conversation);
    }

    private static async Task WriteSseEventAsync(HttpResponse response, ChatStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        var (name, payload) = streamEvent switch
        {
            ChatTokenEvent token => ("token", (object)new { text = token.Text }),
            ChatDoneEvent done => ("done", new
            {
                messageId = done.MessageId,
                status = done.Status,
                citedSourceIds = done.CitedSourceIds,
                evidence = done.Evidence,
            }),
            ChatErrorEvent error => ("error", new
            {
                messageId = error.MessageId,
                category = error.Category,
                message = error.Message,
            }),
            _ => ("token", new { text = string.Empty }),
        };

        var data = JsonSerializer.Serialize(payload, SseJson);
        var frame = new StringBuilder()
            .Append("event: ").Append(name).Append('\n')
            .Append("data: ").Append(data).Append("\n\n")
            .ToString();

        await response.WriteAsync(frame, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

