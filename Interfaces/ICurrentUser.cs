namespace MyFirstApi.Interfaces;

// The logged-in user for the current request (null outside a request / anonymous).
public interface ICurrentUser
{
    string? Username { get; }
}
