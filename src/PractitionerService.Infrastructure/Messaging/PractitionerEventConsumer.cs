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
    private const string _streamKey = "practitioner-events";
    private const string _groupName = "practitioner-service";

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync().ConfigureAwait(false);

        _logger.LogInformation("Practitioner event consumer started. Stream={Stream}, Group={Group}, Consumer={Consumer}", _streamKey, _groupName, _consumerName);

        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await db.StreamReadGroupAsync(_streamKey, _groupName, _consumerName, ">", count: 10).ConfigureAwait(false);

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
            var rawValue = message.Values.FirstOrDefault(v => v.Name == "data").Value;
            string? json = rawValue;

            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Event {Id} не содержит поля 'data'.", message.Id);
                await db.StreamAcknowledgeAsync(_streamKey, _groupName, message.Id).ConfigureAwait(false);
                return;
            }

            var evt = JsonSerializer.Deserialize<PractitionerImportEvent>(json, _jsonOptions);
            if (evt is null || evt.Payload is null)
            {
                _logger.LogWarning("Event {Id}: не удалось разобрать payload.", message.Id);
                await db.StreamAcknowledgeAsync(_streamKey, _groupName, message.Id).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(evt.Type, "created", StringComparison.Ordinal))
            {
                _logger.LogInformation("Event {Id} типа '{Type}' пропущен (поддерживается только 'created').", message.Id, evt.Type);
                await db.StreamAcknowledgeAsync(_streamKey, _groupName, message.Id).ConfigureAwait(false);
                return;
            }

            var payload = evt.Payload;

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CreatePractitionerCommand(
                payload.FirstName,
                payload.LastName,
                payload.Phone,
                payload.Speciality,
                payload.PlaceOfWork,
                payload.Email,
                payload.IsActive,
                SourceEventId: evt.EventId);

            await mediator.Send(command, ct).ConfigureAwait(false);

            await db.StreamAcknowledgeAsync(_streamKey, _groupName, message.Id).ConfigureAwait(false);
            _logger.LogInformation("Event {Id} ({EventId}) успешно обработан.", message.Id, evt.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось обработать event {Id}.", message.Id);

            // For the test task: acknowledge to avoid an infinite retry loop.
            // In production: retry counter + dead-letter queue.
            await db.StreamAcknowledgeAsync(_streamKey, _groupName, message.Id).ConfigureAwait(false);
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = _redis.GetDatabase();
        var created = await db.StreamCreateConsumerGroupAsync(_streamKey, _groupName, "0-0", createStream: true).ConfigureAwait(false);

        if (created)
        {
            _logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'.", _groupName, _streamKey);
        }
        else
        {
            _logger.LogInformation("Consumer group '{Group}' already exists on stream '{Stream}'.", _groupName, _streamKey);
        }
    }
}
