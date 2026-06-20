using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BE_ECOMMERCE.Controllers.Product;

[Route("api/[controller]")]
[ApiController]
public class ProductController(ApplicationDbContext context, IConfiguration configuration) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IConfiguration _configuration = configuration;

    [HttpGet("ai-recommendations")]
    public async Task<IActionResult> GetAIRecommendations()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr))
        {
            // If not logged in, return fallback (best sellers)
            return await GetFallbackRecommendations();
        }

        try
        {
            var aiServiceUrl = _configuration["AiServiceUrl"] ?? "http://localhost:8000";
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{aiServiceUrl}/api/recommend-cf", new
            {
                user_id = userIdStr,
                top_k = 10
            });

            if (response.IsSuccessStatusCode)
            {
                var recommendedIds = await response.Content.ReadFromJsonAsync<List<string>>();
                if (recommendedIds != null && recommendedIds.Any())
                {
                    // Map IDs to actual product data
                    var products = await _context.Products
                        .Where(p => recommendedIds.Contains(p.ProductId))
                        .Select(p => new
                        {
                            articleId = p.ProductId,
                            productName = p.ProductName,
                            price = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.CurrentPrice).FirstOrDefault() : _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                            originalPrice = _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                            discountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                            imageUrl = p.ImageUrl
                        })
                        .ToListAsync();

                    // Maintain the order returned by AI
                    var sortedProducts = recommendedIds
                        .Select(id => products.FirstOrDefault(p => p.articleId == id))
                        .Where(p => p != null)
                        .ToList();

                    return Ok(sortedProducts);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI service: {ex.Message}");
        }

        return await GetFallbackRecommendations();
    }

    [HttpGet("{id}/frequently-bought-together")]
    public async Task<IActionResult> GetFrequentlyBoughtTogether(string id)
    {
        try
        {
            var aiServiceUrl = _configuration["AiServiceUrl"] ?? "http://localhost:8000";
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{aiServiceUrl}/api/recommend-fbt", new
            {
                product_id = id,
                top_k = 5
            });

            if (response.IsSuccessStatusCode)
            {
                var recommendedIds = await response.Content.ReadFromJsonAsync<List<string>>();
                if (recommendedIds != null && recommendedIds.Any())
                {
                    // Map IDs to actual product data
                    var products = await _context.Products
                        .Where(p => recommendedIds.Contains(p.ProductId))
                        .Select(p => new
                        {
                            articleId = p.ProductId,
                            productId = p.ProductId,
                            productName = p.ProductName,
                            price = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.CurrentPrice).FirstOrDefault() : _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                            originalPrice = _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                            discountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                            imageUrl = p.ImageUrl
                        })
                        .ToListAsync();

                    // Maintain the order returned by AI
                    var sortedProducts = recommendedIds
                        .Select(rId => products.FirstOrDefault(p => p.productId == rId))
                        .Where(p => p != null)
                        .ToList();

                    return Ok(sortedProducts);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI FBT service: {ex.Message}");
        }

        return Ok(new List<object>()); // Return empty list on failure
    }

    public class TrackProductViewRequest
    {
        public int DurationInSeconds { get; set; }
    }

    [HttpPost("{id}/track-view")]
    [Authorize]
    public async Task<IActionResult> TrackProductView(string id, [FromBody] TrackProductViewRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        if (request.DurationInSeconds >= 5)
        {
            // Kiểm tra xem đã track interaction này gần đây chưa để tránh spam
            var recentInteraction = await _context.UserInteractions
                .Where(i => i.UserId == userId && i.ProductId == id && i.InteractionType == "VIEW" && i.CreatedAt >= DateTime.UtcNow.AddMinutes(-30))
                .FirstOrDefaultAsync();

            if (recentInteraction == null)
            {
                var interaction = new BE_ECOMMERCE.Entities.UserInteraction
                {
                    UserId = userId,
                    ProductId = id,
                    InteractionType = "VIEW",
                    Score = 1,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserInteractions.Add(interaction);
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new { message = "Interaction tracked" });
    }

    public class ByIdsRequest
    {
        public List<string> ArticleIds { get; set; } = new List<string>();
        public string? SortPrice { get; set; }
    }

    [HttpPost("by-ids")]
    public async Task<IActionResult> GetProductsByIds([FromBody] ByIdsRequest request)
    {
        try
        {
            if (request.ArticleIds == null || !request.ArticleIds.Any())
            {
                return Ok(new { data = new List<object>() });
            }

            var query = _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => request.ArticleIds.Contains(p.ProductId));

            if (request.SortPrice == "asc")
            {
                query = query.OrderBy(p => p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0);
            }
            else if (request.SortPrice == "desc")
            {
                query = query.OrderByDescending(p => p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0);
            }

            var products = await query.Select(p => new
            {
                articleId = p.ProductId,
                productCode = p.ProductId,
                productName = p.ProductName,
                price = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0) : (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0),
                originalPrice = p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0,
                discountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                imageUrl = string.IsNullOrEmpty(p.ImageUrl) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().ImageUrl : "") : p.ImageUrl,
                soldQuantity = p.SoldQuantity
            }).ToListAsync();

            return Ok(new { data = products });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    private async Task<IActionResult> GetFallbackRecommendations()
    {
        var products = await _context.Products
            .OrderByDescending(p => p.SoldQuantity) // Fallback: Best sellers
            .Take(10)
            .Select(p => new
            {
                articleId = p.ProductId,
                productName = p.ProductName,
                price = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.CurrentPrice).FirstOrDefault() : _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                originalPrice = _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => v.OriginalPrice).FirstOrDefault(),
                discountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                imageUrl = p.ImageUrl
            })
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProductDetail(string id)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound("Sản phẩm không tồn tại");

            var firstVariant = product.ProductVariants.FirstOrDefault();

            bool isDiscountActive = product.DiscountPercentage > 0 && (product.DiscountEndDate == null || product.DiscountEndDate >= DateTime.Now);

            return Ok(new
            {
                articleId = product.ProductId,
                productCode = product.ProductId,
                categoryId = product.CategoryId,
                categoryName = product.Categories?.Name,
                parentCategoryName = product.Categories?.ParentId != null ? _context.Categories.FirstOrDefault(c => c.Id == product.Categories.ParentId)?.Name : null,
                productName = product.ProductName,
                price = isDiscountActive && firstVariant != null ? firstVariant.CurrentPrice : (firstVariant != null ? firstVariant.OriginalPrice : 0),
                originalPrice = firstVariant != null ? firstVariant.OriginalPrice : 0,
                discountPercentage = isDiscountActive ? product.DiscountPercentage : 0,
                discountStartDate = product.DiscountStartDate,
                discountEndDate = product.DiscountEndDate,
                favoriteCount = _context.Favorites.Count(f => f.ProductId == product.ProductId),
                imageUrl = product.ImageUrl,
                description = product.Description,
                size = firstVariant?.Size,
                color = firstVariant?.Color,
                stockQuantity = firstVariant != null ? firstVariant.StockQuantity : 0,
                products = product.ProductVariants.Select(v => new
                {
                    articleId = product.ProductId,
                    variantId = v.VariantId,
                    size = v.Size,
                    color = v.Color,
                    stockQuantity = v.StockQuantity,
                    price = v.CurrentPrice,
                    originalPrice = v.OriginalPrice,
                    imageUrl = v.ImageUrl
                }).ToList()
            });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet("flash-sale")]
    public async Task<IActionResult> GetFlashSaleProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActived && p.DiscountPercentage >= 15);

            var totalCount = await query.CountAsync();

            var flashSaleProducts = await query
                .OrderByDescending(p => p.DiscountPercentage)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().ImageUrl : "") : p.ImageUrl,
                    DiscountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                    OriginalPrice = p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0,
                    CurrentPrice = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0) : (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0),
                    SoldQuantity = p.SoldQuantity,
                    StockQuantity = p.ProductVariants.Sum(v => v.StockQuantity)
                })
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalCount,
                TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize),
                Page = page,
                PageSize = pageSize,
                Items = flashSaleProducts
            });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int? categoryId, [FromQuery] string keyword, [FromQuery] string sortBy, [FromQuery] string? sortPrice, [FromQuery] bool? isFlashSale, [FromQuery] bool? isFavorite, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActived);

            if (isFavorite == true)
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
                {
                    query = query.Where(p => _context.Favorites.Any(f => f.ProductId == p.ProductId && f.UserId == userId));
                }
                else
                {
                    // Nếu chưa đăng nhập mà đòi xem yêu thích thì trả về rỗng luôn
                    query = query.Where(p => false);
                }
            }

            if (isFlashSale == true)
            {
                query = query.Where(p => p.DiscountPercentage >= 15);
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.ProductName.Contains(keyword) || (p.Description != null && p.Description.Contains(keyword)));
            }

            if (categoryId.HasValue)
            {
                // Lấy ID của danh mục này và tất cả các danh mục con của nó
                var categoryIds = new System.Collections.Generic.List<int> { categoryId.Value };
                
                // Cấp 2
                var subCategoryIds = await _context.Categories
                    .Where(c => c.ParentId == categoryId.Value)
                    .Select(c => c.Id)
                    .ToListAsync();
                categoryIds.AddRange(subCategoryIds);
                
                // Cấp 3 (leaf)
                if (subCategoryIds.Any()) {
                     var leafIds = await _context.Categories
                        .Where(c => c.ParentId != null && subCategoryIds.Contains(c.ParentId.Value))
                        .Select(c => c.Id)
                        .ToListAsync();
                     categoryIds.AddRange(leafIds);
                }

                query = query.Where(p => p.CategoryId.HasValue && categoryIds.Contains(p.CategoryId.Value));
            }

            var totalCount = await query.CountAsync();

            // Sắp xếp theo Type (Mới nhất, Bán chạy)
            if (sortBy == "best_selling")
            {
                query = query.OrderByDescending(p => p.SoldQuantity);
            }
            else
            {
                if (isFlashSale == true)
                {
                    query = query.OrderByDescending(p => p.DiscountPercentage);
                }
                else 
                {
                    query = query.OrderByDescending(p => p.CreatedAt);
                }
            }

            // Ghi đè bằng Giá nếu có chọn Giá
            // "Mới nhất, bán chạy, yêu thích có thể đi cùng với price"
            // Khi đi cùng với price, price sẽ là ưu tiên sắp xếp chính trên danh sách đã lọc (nếu là yêu thích)
            // hoặc sắp xếp đè lên nếu là best_selling/new (vì ta order by giá).
            if (sortPrice == "ins")
            {
                // Để thay thế OrderBy trước đó, ta phải dùng OrderBy
                query = query.OrderBy(p => p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0);
            }
            else if (sortPrice == "des")
            {
                query = query.OrderByDescending(p => p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0);
            }

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().ImageUrl : "") : p.ImageUrl,
                    DiscountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                    OriginalPrice = p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0,
                    CurrentPrice = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0) : (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0),
                    SoldQuantity = p.SoldQuantity,
                    StockQuantity = p.ProductVariants.Sum(v => v.StockQuantity)
                })
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalCount,
                TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize),
                Page = page,
                PageSize = pageSize,
                Items = products
            });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet("variants")]
    public async Task<IActionResult> GetProductVariants([FromQuery] string productId)
    {
        try
        {
            var query = _context.Set<BE_ECOMMERCE.Entities.Product.ProductVariant>().AsQueryable();
            
            if (!string.IsNullOrEmpty(productId))
            {
                query = query.Where(v => v.ProductId == productId);
            }

            var variants = await query
                .Select(v => new
                {
                    v.VariantId,
                    v.ProductId,
                    v.Color,
                    v.Size,
                    v.OriginalPrice,
                    v.CurrentPrice,
                    v.StockQuantity,
                    v.ImageUrl
                })
                .ToListAsync();

            return Ok(variants);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpPost("search-by-image")]
    public async Task<IActionResult> SearchByImage(Microsoft.AspNetCore.Http.IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest("Không có hình ảnh được tải lên.");

        try
        {
            using var httpClient = new HttpClient();
            using var requestContent = new MultipartFormDataContent();
            using var imageStream = image.OpenReadStream();
            using var streamContent = new StreamContent(imageStream);
            
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(image.ContentType);
            requestContent.Add(streamContent, "image", image.FileName);

            var response = await httpClient.PostAsync("http://localhost:8000/api/predict", requestContent);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Lỗi từ AI Server");
            }

            var rawIds = await response.Content.ReadFromJsonAsync<System.Collections.Generic.List<string>>();
            var recommendedIds = rawIds?.Select(id => System.IO.Path.GetFileNameWithoutExtension(id).Trim().ToLower()).ToList();

            if (recommendedIds == null || recommendedIds.Count == 0)
            {
                return Ok(new { data = new System.Collections.Generic.List<object>() });
            }

            var products = await _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActived && recommendedIds.Contains(p.ProductId.ToLower()))
                .ToListAsync();

            // Sắp xếp lại theo đúng thứ tự của recommendedIds từ Python
            var sortedProducts = products
                .OrderBy(p => recommendedIds.IndexOf(p.ProductId.ToLower()))
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().ImageUrl : "") : p.ImageUrl,
                    DiscountPercentage = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? p.DiscountPercentage : 0,
                    OriginalPrice = p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0,
                    CurrentPrice = (p.DiscountPercentage > 0 && (p.DiscountEndDate == null || p.DiscountEndDate >= DateTime.Now)) ? (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().CurrentPrice : 0) : (p.ProductVariants.FirstOrDefault() != null ? p.ProductVariants.FirstOrDefault().OriginalPrice : 0),
                    SoldQuantity = p.SoldQuantity,
                    p.CategoryId
                })
                .ToList();

            return Ok(new { data = sortedProducts });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }
}