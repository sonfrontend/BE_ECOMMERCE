using System.ComponentModel.DataAnnotations;

namespace BE_ECOMMERCE.Entities.Order;

public class ResolutionTemplate : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } // Ghi chú cách thức xử lý cho Admin

    public bool RestoresInventory { get; set; } = false;
    public bool IsFullRefund { get; set; } = false;
    public bool RequiresRefund { get; set; } = false;

    [MaxLength(50)]
    public string? FinalOrderStatus { get; set; }
}
