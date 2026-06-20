using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers.Product
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu đăng nhập mới được dùng các API này
    public class FavoriteController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpPost("toggle/{productId}")]
        public async Task<IActionResult> ToggleFavorite(string productId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập" });
            }

            // Kiểm tra product có tồn tại không
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm" });
            }

            var existingFavorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

            bool isFavorited = false;
            if (existingFavorite != null)
            {
                // Đã thích -> Bỏ thích
                _context.Favorites.Remove(existingFavorite);
                isFavorited = false;
            }
            else
            {
                // Chưa thích -> Thích
                var newFavorite = new Favorite
                {
                    UserId = userId,
                    ProductId = productId
                };
                _context.Favorites.Add(newFavorite);
                isFavorited = true;

                // Ghi nhận tương tác Add to Favorite
                _context.UserInteractions.Add(new BE_ECOMMERCE.Entities.UserInteraction
                {
                    UserId = userId,
                    ProductId = productId,
                    InteractionType = "FAVORITE",
                    Score = 2,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Đếm lại tổng số lượt thích
            var newCount = await _context.Favorites.CountAsync(f => f.ProductId == productId);

            return Ok(new
            {
                isFavorited,
                favoriteCount = newCount,
                message = isFavorited ? "Đã thêm vào yêu thích" : "Đã bỏ yêu thích"
            });
        }

        [HttpGet("my-favorites")]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized();
            }

            var favoriteProductIds = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.ProductId)
                .ToListAsync();

            return Ok(favoriteProductIds);
        }

        [HttpGet("my-favorites-details")]
        public async Task<IActionResult> GetMyFavoritesDetails()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized();
            }

            var favorites = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => new
                {
                    productId = f.Product.ProductId,
                    productName = f.Product.ProductName,
                    imageUrl = f.Product.ImageUrl,
                    soldQuantity = f.Product.SoldQuantity,
                    discountPercentage = (f.Product.DiscountPercentage > 0 && (f.Product.DiscountEndDate == null || f.Product.DiscountEndDate >= DateTime.UtcNow)) ? f.Product.DiscountPercentage : 0,
                    originalPrice = f.Product.ProductVariants.FirstOrDefault() != null ? f.Product.ProductVariants.FirstOrDefault().OriginalPrice : 0,
                    currentPrice = (f.Product.DiscountPercentage > 0 && (f.Product.DiscountEndDate == null || f.Product.DiscountEndDate >= DateTime.UtcNow)) 
                        ? (f.Product.ProductVariants.FirstOrDefault() != null ? f.Product.ProductVariants.FirstOrDefault().CurrentPrice : 0) 
                        : (f.Product.ProductVariants.FirstOrDefault() != null ? f.Product.ProductVariants.FirstOrDefault().OriginalPrice : 0),
                    rating = Math.Round(_context.Reviews.Where(r => r.ProductId == f.Product.ProductId).Average(r => (double?)r.Rating) ?? 5.0, 1),
                    reviewsCount = _context.Reviews.Count(r => r.ProductId == f.Product.ProductId),
                    likesCount = _context.Favorites.Count(fav => fav.ProductId == f.Product.ProductId)
                })
                .ToListAsync();

            return Ok(favorites);
        }
    }
}
