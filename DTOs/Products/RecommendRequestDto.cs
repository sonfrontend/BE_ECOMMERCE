using System.Collections.Generic;

namespace BE_ECOMMERCE.DTOs.Products
{
    public class RecommendRequestDto
    {
        public List<string> ProductIds { get; set; } = new List<string>();
        public int TopK { get; set; } = 15;
    }
}
