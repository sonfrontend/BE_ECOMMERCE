namespace BE_ECOMMERCE.Entities.Auth;

public class BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; } = null;

    public bool IsActived { get; set; } = true;
}