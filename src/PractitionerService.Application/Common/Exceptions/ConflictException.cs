public sealed class ConflictException : Exception
{
    public ConflictException(string message, Exception? inner = null) : base(message, inner) { }
}