using System.ComponentModel.DataAnnotations;

namespace BE_ECOMMERCE.DTOs.Review
{
    public class CreateReviewRequest
    {
        [Required]
        public string ProductId { get; set; }

        [Required]
        public int OrderItemId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string Comment { get; set; }
    }
}
