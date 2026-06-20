using System;
using System.Collections.Generic;

namespace BE_ECOMMERCE.Entities.Product
{
    public class Product : BaseEntity
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public int? CategoryId { get; set; }
        public virtual BE_ECOMMERCE.Entities.Category.Category Categories { get; set; }
        public string Description { get; set; }
        public int? DiscountPercentage { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public string ImageUrl { get; set; }
        public int SoldQuantity { get; set; }

        public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
    }
}