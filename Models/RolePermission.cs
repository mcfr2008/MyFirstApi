namespace MyFirstApi.Models;

public class RolePermission
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
