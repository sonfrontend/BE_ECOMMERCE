using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.DTOs.Review;
using BE_ECOMMERCE.Entities.Product;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace BE_ECOMMERCE.Controllers.Product
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized("User not logged in");

            // Kiểm tra xem OrderItem có tồn tại và thuộc về User này, và đơn hàng đã Completed chưa
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant)
                .FirstOrDefaultAsync(oi => oi.Id == request.OrderItemId);

            if (orderItem == null)
                return NotFound("Order item not found");

            if (orderItem.Order.UserId != userId)
                return Forbid("You do not own this order item");

            if (orderItem.Order.Status != OrderStatus.Completed)
                return BadRequest("You can only review completed orders");

            if (orderItem.ProductVariant.ProductId != request.ProductId)
                return BadRequest("Product ID mismatch");

            // Kiểm tra xem đã review chưa
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.OrderItemId == request.OrderItemId);

            if (existingReview != null)
                return BadRequest("You have already reviewed this item");

            var review = new Review
            {
                UserId = userId,
                ProductId = request.ProductId,
                OrderItemId = request.OrderItemId,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = DateTime.UtcNow,
                IsActived = true
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Review added successfully", reviewId = review.Id });
        }

        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(string productId)
        {
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId && r.IsActived)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDTO
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.FullName ?? r.User.UserName,
                    AvatarUrl = r.User.AvatarUrl,
                    ProductId = r.ProductId,
                    OrderItemId = r.OrderItemId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            var totalReviews = reviews.Count;
            var averageRating = totalReviews > 0 ? reviews.Average(r => r.Rating) : 0;

            return Ok(new
            {
                TotalReviews = totalReviews,
                AverageRating = Math.Round(averageRating, 1),
                Reviews = reviews
            });
        }

        [HttpGet("my-reviews")]
        [Authorize]
        public async Task<IActionResult> GetMyReviews()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized("User not logged in");

            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Where(r => r.UserId == userId && r.IsActived)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.ProductId,
                    ProductName = r.Product.ProductName,
                    ProductImage = r.Product.ImageUrl,
                    r.OrderItemId,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(reviews);
        }

        [HttpGet("pending-reviews")]
        [Authorize]
        public async Task<IActionResult> GetPendingReviews()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized("User not logged in");

            // Find order items that belong to completed orders, and do NOT have a review yet
            var pendingOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant)
                .ThenInclude(pv => pv.Product)
                .Where(oi => oi.Order.UserId == userId && oi.Order.Status == OrderStatus.Completed)
                .Where(oi => !_context.Reviews.Any(r => r.OrderItemId == oi.Id && r.IsActived))
                .OrderByDescending(oi => oi.Order.DeliveredDate ?? oi.Order.OrderDate)
                .Select(oi => new
                {
                    OrderItemId = oi.Id,
                    OrderId = oi.OrderId,
                    OrderDate = oi.Order.OrderDate,
                    DeliveredDate = oi.Order.DeliveredDate,
                    ProductId = oi.ProductVariant.ProductId,
                    ProductName = oi.ProductVariant.Product.ProductName,
                    ProductImage = oi.ProductVariant.Product.ImageUrl,
                    VariantName = oi.ProductVariant.Color + " - " + oi.ProductVariant.Size
                })
                .ToListAsync();

            return Ok(pendingOrderItems);
        }

        // --- ADMIN ENDPOINTS ---
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminReviews()
        {
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    UserName = r.User.FullName ?? r.User.UserName,
                    AvatarUrl = r.User.AvatarUrl,
                    r.ProductId,
                    ProductName = r.Product.ProductName,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    r.IsActived
                })
                .ToListAsync();

            return Ok(reviews);
        }

        [HttpDelete("admin/{id}")]
        public async Task<IActionResult> DeleteAdminReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound("Review not found");

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Review deleted successfully" });
        }
    }
}

