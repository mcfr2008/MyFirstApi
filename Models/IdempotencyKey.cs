namespace MyFirstApi.Models;

// The stored response of a POST sent with an Idempotency-Key header, so a retry
// (lost connection, scanner resend) gets the same answer instead of running again.
public class IdempotencyKey
{
    public string Username { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    // SHA-256 (hex) of method, path and the bound request data.
    public string RequestHash { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseLocation { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
