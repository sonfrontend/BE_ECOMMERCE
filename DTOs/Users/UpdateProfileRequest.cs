namespace BE_ECOMMERCE.DTOs.Users;

public class UpdateProfileRequest
{
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Password { get; set; }
    public string OldPassword { get; set; }
    public string AvatarUrl { get; set; }
}