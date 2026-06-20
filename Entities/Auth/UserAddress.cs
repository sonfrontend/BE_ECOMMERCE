using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.Auth;

public class UserAddress : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    [Required]
    [MaxLength(200)]
    public string RecipientName { get; set; }

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Address { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    public bool IsDefault { get; set; } = false;
}
