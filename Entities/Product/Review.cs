using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BE_ECOMMERCE.Entities.Auth;
using BE_ECOMMERCE.Entities.Order;

namespace BE_ECOMMERCE.Entities.Product;

public class Review : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    [Required]
    public string ProductId { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; }

    // Ràng buộc với OrderItem để đảm bảo mỗi sản phẩm mua trong 1 đơn chỉ được đánh giá 1 lần
    [Required]
    public int OrderItemId { get; set; }

    [ForeignKey("OrderItemId")]
    public virtual OrderItem OrderItem { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string Comment { get; set; }
}
