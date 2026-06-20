using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.System;

public class Notification : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string Title { get; set; }

    [Required]
    public string Message { get; set; }

    public bool IsRead { get; set; } = false;

    // Type có thể là: "OrderCreated", "OrderStatusChanged", "System", "Promotion"
    public string Type { get; set; }

    // Có thể lưu Id của đơn hàng (OrderId) để FE điều hướng
    public string? RelatedId { get; set; }

    [ForeignKey("UserId")]
    public Auth.User User { get; set; }
}
