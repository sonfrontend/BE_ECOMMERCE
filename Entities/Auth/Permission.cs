
namespace BE_ECOMMERCE.Entities.Auth;

public class Permission : BaseEntity
{
    public Guid PermissionId { get; set; }
    public string PermissionName { get; set; }
    public string Description { get; set; }


}