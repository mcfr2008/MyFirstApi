namespace MyFirstApi.Models;

// Common shape of every master-data table (codes are unique, stored upper-case;
// rows are deactivated instead of deleted so existing references stay valid).
public interface IMasterData
{
    int Id { get; set; }
    string Code { get; set; }
    bool IsActive { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
