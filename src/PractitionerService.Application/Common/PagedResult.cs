namespace PractitionerService.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Offset, int Count);
