
namespace BE_ECOMMERCE.Entities.Auth;

public class User : BaseEntity
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    public string GoogleId { get; set; }
    public string AvatarUrl { get; set; }
    public string PhoneNumber { get; set; }

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

}