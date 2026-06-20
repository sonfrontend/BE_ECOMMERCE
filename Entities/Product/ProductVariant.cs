using System;

namespace BE_ECOMMERCE.Entities.Product
{
    public class ProductVariant : BaseEntity
    {
        public int VariantId { get; set; }
        public string ProductId { get; set; }
        public virtual Product Product { get; set; }
        public string SKU { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public int StockQuantity { get; set; }
        public int SoldQuantity { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public string ImageUrl { get; set; }
    }
}
