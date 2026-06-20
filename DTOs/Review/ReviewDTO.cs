using System;

namespace BE_ECOMMERCE.DTOs.Review
{
    public class ReviewDTO
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string ProductId { get; set; }
        public int OrderItemId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
