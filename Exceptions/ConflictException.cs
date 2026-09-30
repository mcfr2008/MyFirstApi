namespace MyFirstApi.Exceptions;

// Thrown by services when a request clashes with existing data
// (e.g. a duplicate code); ApiExceptionHandler turns it into 409 Conflict.
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
