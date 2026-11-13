using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Practitioners.Events;
using StackExchange.Redis;

namespace PractitionerService.Infrastructure.Messaging;

internal sealed class PractitionerEventConsumer : BackgroundService
{
    private const string StreamKey = "practitioner-events";
    private const string GroupName = "practitioner-service";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string ConsumerName = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";

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
        await EnsureConsumerGroupAsync(ct);

        _logger.LogInformation("Practitioner event consumer started. Stream={Stream}, Group={Group}, Consumer={Consumer}", StreamKey, GroupName, ConsumerName);

        var db = _redis.GetDatabase();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var messages = await db.StreamReadGroupAsync(StreamKey, GroupName, ConsumerName, ">", count: 10);

                if (messages.Length == 0)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                foreach (var message in messages)
                    await ProcessAsync(db, message, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении событий из Redis Stream.");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessAsync(IDatabase db, StreamEntry message, CancellationToken ct)
    {
        try
        {
            var rawValue = message.Values.FirstOrDefault(v => v.Name == "data").Value;
            string? json = rawValue;

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Event {Id} не содержит поля 'data'.", message.Id);
                await db.StreamAcknowledgeAsync(StreamKey, GroupName, message.Id);
                return;
            }

            var evt = JsonSerializer.Deserialize<PractitionerImportEvent>(json!, JsonOptions);
            if (evt is null || evt.Payload is null)
            {
                _logger.LogWarning("Event {Id}: не удалось разобрать payload.", message.Id);
                await db.StreamAcknowledgeAsync(StreamKey, GroupName, message.Id);
                return;
            }

            if (evt.Type != "created")
            {
                _logger.LogInformation("Event {Id} типа '{Type}' пропущен (поддерживается только 'created').", message.Id, evt.Type);
                await db.StreamAcknowledgeAsync(StreamKey, GroupName, message.Id);
                return;
            }

            var payload = evt.Payload;

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CreatePractitionerCommand(
                payload.FirstName, payload.LastName, payload.Phone,
                payload.Speciality, payload.PlaceOfWork, payload.Email,
                payload.IsActive, SourceEventId: evt.EventId);

            await mediator.Send(command, ct);

            await db.StreamAcknowledgeAsync(StreamKey, GroupName, message.Id);
            _logger.LogInformation("Event {Id} ({EventId}) успешно обработан.", message.Id, evt.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось обработать event {Id}.", message.Id);
            // For the test task: acknowledge to avoid an infinite retry loop.
            // In production: retry counter + dead-letter queue.
            await db.StreamAcknowledgeAsync(StreamKey, GroupName, message.Id);
        }
    }

    private async Task EnsureConsumerGroupAsync(CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var created = await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, "0-0", createStream: true);

        if (created)
        {
            _logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'.", GroupName, StreamKey);
        }
        else
        {
            _logger.LogInformation("Consumer group '{Group}' already exists on stream '{Stream}'.", GroupName, StreamKey);
        }
    }
}