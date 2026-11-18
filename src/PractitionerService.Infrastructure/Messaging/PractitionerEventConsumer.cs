using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Common.Diagnostics;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Application.Practitioners;
using PractitionerService.Application.Practitioners.Events;
using PractitionerService.Domain.Common.Exceptions;
using StackExchange.Redis;

namespace PractitionerService.Infrastructure.Messaging;

internal sealed class PractitionerEventConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";

    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPractitionerMetrics _metrics;
    private readonly ILogger<PractitionerEventConsumer> _logger;

    public PractitionerEventConsumer(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory, IPractitionerMetrics metrics, ILogger<PractitionerEventConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync().ConfigureAwait(false);

        _logger.LogInformation("Consumer событий врачей запущен. Stream={Stream}, Group={Group}, Consumer={Consumer}", EventConstants.StreamKey, EventConstants.GroupName, _consumerName);

        var db = _redis.GetDatabase();
        var lastReclaim = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Periodic reclaim of stale pending messages
                if (DateTime.UtcNow - lastReclaim > EventConstants.ReclaimMinIdle)
                {
                    await ReclaimStaleAsync(db, stoppingToken).ConfigureAwait(false);
                    lastReclaim = DateTime.UtcNow;
                }

                var messages = await db.StreamReadGroupAsync(EventConstants.StreamKey, EventConstants.GroupName, _consumerName, ">", count: 10).ConfigureAwait(false);

                if (messages.Length == 0)
                {
                    await Task.Delay(500, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var message in messages)
                {
                    await ProcessAsync(db, message, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении событий из Redis Stream.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessAsync(IDatabase db, StreamEntry message, CancellationToken ct)
    {
        try
        {
            string? json = message.Values.FirstOrDefault(v => string.Equals(v.Name.ToString(), EventConstants.PayloadFieldName, StringComparison.Ordinal)).Value;

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Event {Id} не содержит поля '{Field}'. Сообщение отброшено.", message.Id, EventConstants.PayloadFieldName);
                await AcknowledgeAndCleanupAsync(db, message.Id).ConfigureAwait(false);
                _metrics.EventProcessed("skipped");
                return;
            }

            PractitionerImportEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<PractitionerImportEvent>(json, _jsonOptions);
            }
            catch (JsonException ex)
            {
                // Malformed JSON never becomes valid — ACK + DLQ for inspection.
                _logger.LogError(ex, "Event {Id}: некорректный JSON (длина {Length} симв.). Сообщение перемещено в DLQ.", message.Id, json.Length);
                await MoveToDlqAsync(db, message, "invalid-json").ConfigureAwait(false);
                _metrics.EventProcessed("business-error");
                return;
            }

            if (evt is null || evt.Payload is null)
            {
                _logger.LogWarning("Event {Id}: JSON валиден, но payload отсутствует. Сообщение перемещено в DLQ.", message.Id);
                await MoveToDlqAsync(db, message, "missing-payload").ConfigureAwait(false);
                _metrics.EventProcessed("business-error");
                return;
            }

            if (!string.Equals(evt.Type, EventConstants.TypeCreated, StringComparison.Ordinal))
            {
                _logger.LogInformation("Event {Id} типа '{Type}' пропущен (поддерживается только '{Supported}').", message.Id, evt.Type, EventConstants.TypeCreated);
                await AcknowledgeAndCleanupAsync(db, message.Id).ConfigureAwait(false);
                _metrics.EventProcessed("skipped");
                return;
            }

            var payload = evt.Payload;

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CreatePractitionerCommand(payload.FirstName, payload.LastName, payload.Phone, payload.Speciality, payload.PlaceOfWork, payload.Email, payload.IsActive, SourceEventId: evt.EventId);

            await mediator.Send(command, ct).ConfigureAwait(false);

            await AcknowledgeAndCleanupAsync(db, message.Id).ConfigureAwait(false);
            _metrics.EventProcessed("success");
            _logger.LogInformation("Event {Id} ({EventId}) успешно обработан.", message.Id, evt.EventId);
        }
        catch (Exception ex) when (IsBusinessError(ex))
        {
            // Business errors (validation, duplicates) won't become valid on retry → DLQ.
            _logger.LogWarning(ex, "Event {Id}: бизнес-ошибка. Сообщение перемещено в DLQ.", message.Id);
            await MoveToDlqAsync(db, message, "business-error").ConfigureAwait(false);
            _metrics.EventProcessed("business-error");
        }
        catch (Exception ex)
        {
            // Infrastructure errors (DB down, timeout): do NOT ACK — let XAUTOCLAIM retry.
            _logger.LogError(ex, "Event {Id}: временная ошибка. Сообщение остаётся в PEL для повторной обработки.", message.Id);
            _metrics.EventFailed("infrastructure");
        }
    }

    private static bool IsBusinessError(Exception ex) => ex is DomainException
        or FhirValidationException
        or NotFoundException
        or GoneException
        or ConflictException
        or UniqueConstraintViolationException;

    private async Task ReclaimStaleAsync(IDatabase db, CancellationToken ct)
    {
        try
        {
            var result = await db.StreamAutoClaimAsync(EventConstants.StreamKey, EventConstants.GroupName, _consumerName, (long)EventConstants.ReclaimMinIdle.TotalMilliseconds, "0-0", count: 10).ConfigureAwait(false);

            if (result.ClaimedEntries.Length == 0)
            {
                return;
            }

            _logger.LogInformation("Перехвачено {Count} зависших сообщений для повторной обработки.", result.ClaimedEntries.Length);

            foreach (var message in result.ClaimedEntries)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await HandleReclaimedAsync(db, message, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось перехватить зависшие сообщения (XAUTOCLAIM).");
        }
    }

    private async Task HandleReclaimedAsync(IDatabase db, StreamEntry message, CancellationToken ct)
    {
        var attemptsKey = EventConstants.AttemptsKeyPrefix + message.Id;
        var attempts = await db.HashIncrementAsync(attemptsKey, "count").ConfigureAwait(false);
        await db.KeyExpireAsync(attemptsKey, EventConstants.AttemptsKeyTtl).ConfigureAwait(false);

        if (attempts > EventConstants.MaxRetries)
        {
            _logger.LogError("Event {Id}: превышено {Max} попыток ({Attempts}). Перемещено в DLQ.", message.Id, EventConstants.MaxRetries, attempts);
            await MoveToDlqAsync(db, message, "retries-exhausted").ConfigureAwait(false);
            _metrics.EventFailed("exhausted");
            return;
        }

        _logger.LogWarning("Event {Id}: повторная попытка {Attempt}/{Max}.", message.Id, attempts, EventConstants.MaxRetries);

        await ProcessAsync(db, message, ct).ConfigureAwait(false);
    }

    private async Task AcknowledgeAndCleanupAsync(IDatabase db, RedisValue messageId)
    {
        await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, messageId).ConfigureAwait(false);
        await db.KeyDeleteAsync(EventConstants.AttemptsKeyPrefix + messageId).ConfigureAwait(false);
    }

    private async Task MoveToDlqAsync(IDatabase db, StreamEntry message, string reason)
    {
        var dlqEntry = new NameValueEntry[]
        {
            new(EventConstants.PayloadFieldName, message.Values.FirstOrDefault(v => string.Equals(v.Name.ToString(), EventConstants.PayloadFieldName, StringComparison.Ordinal)).Value),
            new("originalMessageId", message.Id.ToString()),
            new("reason", reason),
            new("movedAt", DateTime.UtcNow.ToString("O")),
        };

        await db.StreamAddAsync(EventConstants.DeadLetterStreamKey, dlqEntry).ConfigureAwait(false);
        await AcknowledgeAndCleanupAsync(db, message.Id).ConfigureAwait(false);
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = _redis.GetDatabase();
        var created = await db.StreamCreateConsumerGroupAsync(EventConstants.StreamKey, EventConstants.GroupName, "0-0", createStream: true).ConfigureAwait(false);

        if (created)
        {
            _logger.LogInformation("Создана consumer group '{Group}' в stream '{Stream}'.", EventConstants.GroupName, EventConstants.StreamKey);
        }
        else
        {
            _logger.LogInformation("Consumer group '{Group}' уже существует в stream '{Stream}'.", EventConstants.GroupName, EventConstants.StreamKey);
        }
    }
}
