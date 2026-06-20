using BE_ECOMMERCE.Entities.Auth;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.Promotion
{
    [Table("UserPromotions")]
    public class UserPromotion : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public int PromotionId { get; set; }
        [ForeignKey("PromotionId")]
        public virtual Promotion? Promotion { get; set; }

        public int? OrderId { get; set; }
        [ForeignKey("OrderId")]
        public virtual Entities.Order.Order? Order { get; set; }

        public bool IsUsed { get; set; } = true;
    }
}
