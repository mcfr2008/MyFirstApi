namespace MyFirstApi.Exceptions;

// Thrown by services when a request is well-formed but breaks a business rule
// (e.g. referencing an inactive location); ApiExceptionHandler turns it into 400.
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
