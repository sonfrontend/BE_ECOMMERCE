using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BE_ECOMMERCE.Entities.Auth;

namespace BE_ECOMMERCE.Entities.Order;

[Table("Complaints")]
public class Complaint : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }
    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    public bool RestoresInventory { get; set; } = false;
    public bool IsFullRefund { get; set; } = false;
    public bool RequiresRefund { get; set; } = false;

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; }

    [MaxLength(2000)]
    public string? EvidenceUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Resolved

    public int? HandlingMethodId { get; set; }
    [ForeignKey("HandlingMethodId")]
    public virtual ResolutionTemplate? ResolutionTemplate { get; set; }

    public decimal? RefundAmount { get; set; }

    [MaxLength(50)]
    public string? FinalOrderStatus { get; set; }

    [MaxLength(1000)]
    public string? AdminNote { get; set; }

    [MaxLength(2000)]
    public string? AdminEvidenceUrl { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
