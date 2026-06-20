using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.Order;

[Table("ShippingFees")]
public class ShippingFee
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProvinceName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Fee { get; set; }
}
