namespace PractitionerService.Application.Common;

public static class CacheKeys
{
    public static string PractitionerById(Guid id) => $"practitioner:id:{id}";
}
