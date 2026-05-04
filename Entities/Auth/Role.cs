
namespace BE_ECOMMERCE.Entities.Auth;

public class Role : BaseEntity
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }

}