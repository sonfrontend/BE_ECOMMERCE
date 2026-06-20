using System;

namespace BE_ECOMMERCE.DTOs.Carts
{
    public class AddToCartRequest
    {
        public string ArticleId { get; set; }
        public int? VariantId { get; set; }
        public int Quantity { get; set; }
    }
}
