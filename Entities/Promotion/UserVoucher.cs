using BE_ECOMMERCE.Entities.Auth;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.Promotion
{
    [Table("UserVouchers")]
    public class UserVoucher : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public int VoucherId { get; set; }
        [ForeignKey("VoucherId")]
        public virtual Voucher? Voucher { get; set; }

        public bool IsUsed { get; set; } = false;

        public int? OrderId { get; set; }
        [ForeignKey("OrderId")]
        public virtual Entities.Order.Order? Order { get; set; }
    }
}
