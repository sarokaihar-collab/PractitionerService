namespace PractitionerService.Application.Common.Exceptions;

public sealed class GoneException : Exception
{
    public GoneException(string message)
        : base(message)
    {
    }
}
