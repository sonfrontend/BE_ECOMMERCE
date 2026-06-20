using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_ECOMMERCE.Entities.Order;

[Table("ComplaintReasons")]
public class ComplaintReason : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; }

    public bool IsActive { get; set; } = true;
}
