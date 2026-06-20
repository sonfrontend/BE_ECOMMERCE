using System;
using System.ComponentModel.DataAnnotations;

namespace BE_ECOMMERCE.Entities.Promotion;

public class Voucher : BaseEntity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; }

    [Required]
    public decimal DiscountValue { get; set; }

    public decimal MinOrderValue { get; set; } = 0;

    public DateTime StartDate { get; set; } = DateTime.Now;
    
    public DateTime EndDate { get; set; } = DateTime.Now.AddMonths(1);

    public int Quantity { get; set; } = 0;
}
