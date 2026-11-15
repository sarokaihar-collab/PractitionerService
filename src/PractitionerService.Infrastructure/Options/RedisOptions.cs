using System.ComponentModel.DataAnnotations;

namespace PractitionerService.Infrastructure.Options;

/// <summary>
/// Redis connection options. Bound from the "Redis" configuration section.
/// </summary>
public sealed class RedisOptions
{
    public const string Section = "Redis";

    /// <summary>StackExchange.Redis configuration string (e.g. "localhost:6380").</summary>
    [Required(ErrorMessage = "Строка подключения к Redis (Redis:Configuration) обязательна.")]
    public string Configuration { get; set; } = string.Empty;

    /// <summary>Key prefix for IDistributedCache entries.</summary>
    public string InstanceName { get; set; } = "practitioner-service:";
}