using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities
{
    [Table("UserInteractions")]
    public class UserInteraction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InteractionType { get; set; } // VIEW, CART, PURCHASE, FAVORITE

        [Required]
        public int Score { get; set; } // e.g. 1

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
