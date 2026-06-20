using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace BE_ECOMMERCE.Entities.Category;

public class Category : BaseEntity
{
    public int Id { get; set; }

    [MaxLength(255)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    // EF Core sẽ tự động hiểu ParentId là khóa ngoại của ParentCategory
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public virtual Category ParentCategory { get; set; }

    public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public virtual ICollection<BE_ECOMMERCE.Entities.Product.Product> Products { get; set; } = new List<BE_ECOMMERCE.Entities.Product.Product>();
}