namespace MyFirstApi.Exceptions;

// Thrown by services for expected failures (business rules, conflicts).
// ApiExceptionHandler turns it into a ProblemDetails response with the
// error's status, English detail, "code" and "args" (for Thai / other UI text).
// Create instances through the factory methods in Errors.
public class ApiException : Exception
{
    public ApiException(ErrorDefinition definition, IReadOnlyDictionary<string, object?> args, ErrorScope? scope = null)
        : base((scope?.EnPrefix ?? "") + Render(definition.En, args))
    {
        Definition = definition;
        Args = args;
        Scope = scope;
    }

    public ErrorDefinition Definition { get; }
    public IReadOnlyDictionary<string, object?> Args { get; }
    public ErrorScope? Scope { get; }

    // Same error, tagged with the batch entry it came from.
    public ApiException In(ErrorScope? scope) => scope == null ? this : new ApiException(Definition, Args, scope);

    public static string Render(string template, IReadOnlyDictionary<string, object?> args)
    {
        var text = template;
        foreach (var (name, value) in args)
        {
            var rendered = value is IEnumerable<string> list ? string.Join(", ", list) : value?.ToString() ?? "";
            text = text.Replace("{" + name + "}", rendered);
        }
        return text;
    }
}
