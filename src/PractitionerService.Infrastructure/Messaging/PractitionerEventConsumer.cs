using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Practitioners;
using PractitionerService.Application.Practitioners.Events;
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
    private readonly ILogger<PractitionerEventConsumer> _logger;

    public PractitionerEventConsumer(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory, ILogger<PractitionerEventConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await EnsureConsumerGroupAsync().ConfigureAwait(false);

        _logger.LogInformation("Consumer событий врачей запущен. Stream={Stream}, Group={Group}, Consumer={Consumer}", EventConstants.StreamKey, EventConstants.GroupName, _consumerName);

        var db = _redis.GetDatabase();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var messages = await db.StreamReadGroupAsync(EventConstants.StreamKey, EventConstants.GroupName, _consumerName, ">", count: 10).ConfigureAwait(false);

                if (messages.Length == 0)
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                    continue;
                }

                foreach (var message in messages)
                {
                    await ProcessAsync(db, message, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении событий из Redis Stream.");
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessAsync(IDatabase db, StreamEntry message, CancellationToken ct)
    {
        try
        {
            var rawValue = message.Values.FirstOrDefault(v => string.Equals(v.Name.ToString(), EventConstants.PayloadFieldName, StringComparison.Ordinal)).Value;

            string? json = rawValue;

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Event {Id} не содержит поля '{Field}'.", message.Id, EventConstants.PayloadFieldName);
                await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
                return;
            }

            PractitionerImportEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<PractitionerImportEvent>(json, _jsonOptions);
            }
            catch (JsonException ex)
            {
                // Do NOT log the raw JSON: it may contain PII (firstName, lastName, phone, email). Log only the length + event id for correlation.
                _logger.LogError(ex, "Event {Id}: некорректный JSON (длина {Length} симв.). Содержимое не логируется во избежание утечки PII.", message.Id, json.Length);
                await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
                return;
            }

            if (evt is null || evt.Payload is null)
            {
                _logger.LogWarning("Event {Id}: JSON валиден, но payload отсутствует.", message.Id);
                await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(evt.Type, EventConstants.TypeCreated, StringComparison.Ordinal))
            {
                _logger.LogInformation("Event {Id} типа '{Type}' пропущен (поддерживается только '{Supported}').", message.Id, evt.Type, EventConstants.TypeCreated);
                await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
                return;
            }

            var payload = evt.Payload;

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CreatePractitionerCommand(payload.FirstName, payload.LastName, payload.Phone, payload.Speciality, payload.PlaceOfWork, payload.Email, payload.IsActive, SourceEventId: evt.EventId);

            await mediator.Send(command, ct).ConfigureAwait(false);

            await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
            _logger.LogInformation("Event {Id} ({EventId}) успешно обработан.", message.Id, evt.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось обработать event {Id}.", message.Id);

            // For the test task: acknowledge to avoid an infinite retry loop. In production: retry counter + dead-letter queue.
            await db.StreamAcknowledgeAsync(EventConstants.StreamKey, EventConstants.GroupName, message.Id).ConfigureAwait(false);
        }
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
