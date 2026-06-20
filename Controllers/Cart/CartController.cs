using System.Security.Claims;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.DTOs.Carts;
using BE_ECOMMERCE.Entities.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers.Cart;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CartController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var cartItems = await _context.CartItems
            .Include(c => c.ProductVariant)
            .ThenInclude(pv => pv.Product)
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                id = c.Id,
                quantity = c.Quantity,
                product = new
                {
                    articleId = c.ProductVariant.ProductId,
                    productName = c.ProductVariant.Product.ProductName,
                    price = (c.ProductVariant.Product.DiscountPercentage > 0 && (c.ProductVariant.Product.DiscountEndDate == null || c.ProductVariant.Product.DiscountEndDate >= DateTime.Now)) 
                        ? (c.ProductVariant.CurrentPrice > 0 ? c.ProductVariant.CurrentPrice : c.ProductVariant.OriginalPrice) 
                        : c.ProductVariant.OriginalPrice,
                    originalPrice = c.ProductVariant.OriginalPrice,
                    imageUrl = c.ProductVariant.ImageUrl,
                    color = c.ProductVariant.Color,
                    size = c.ProductVariant.Size
                }
            })
            .ToListAsync();

        return Ok(cartItems);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        // Tìm variant cụ thể nếu có VariantId
        Entities.Product.ProductVariant variant = null;
        if (request.VariantId.HasValue && request.VariantId.Value > 0)
        {
            variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.VariantId == request.VariantId.Value);
        }
        else
        {
            variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.ProductId == request.ArticleId);
        }
        if (variant == null)
            return NotFound("Sản phẩm không tồn tại");

        var cartItem = await _context.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.VariantId == variant.VariantId);
        int currentQuantity = cartItem != null ? cartItem.Quantity : 0;
        
        if (currentQuantity + request.Quantity > variant.StockQuantity)
            return BadRequest($"Sản phẩm này chỉ còn {variant.StockQuantity} cái trong kho");

        if (cartItem != null)
            cartItem.Quantity += request.Quantity;
        else
        {
            cartItem = new CartItem
            {
                UserId = userId,
                VariantId = variant.VariantId,
                Quantity = request.Quantity
            };
            _context.CartItems.Add(cartItem);
        }
        await _context.SaveChangesAsync();

        // Ghi nhận tương tác Add to Cart
        _context.UserInteractions.Add(new BE_ECOMMERCE.Entities.UserInteraction
        {
            UserId = userId,
            ProductId = request.ArticleId,
            InteractionType = "CART",
            Score = 3,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return Ok("Đã thêm vào giỏ hàng");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCartItem(int id, [FromBody] int quantity)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        if (quantity < 1)
            return BadRequest("Số lượng không hợp lệ");

        var cartItem = await _context.CartItems
            .Include(c => c.ProductVariant)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            
        if (cartItem == null)
            return NotFound("Không tìm thấy sản phẩm trong giỏ");

        if (quantity > cartItem.ProductVariant.StockQuantity)
            return BadRequest($"Sản phẩm này chỉ còn {cartItem.ProductVariant.StockQuantity} cái trong kho");

        cartItem.Quantity = quantity;
        await _context.SaveChangesAsync();
        
        return Ok("Đã cập nhật số lượng");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFromCart(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var cartItem = await _context.CartItems.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (cartItem == null)
            return NotFound("Không tìm thấy sản phẩm trong giỏ");

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return Ok("Đã xóa sản phẩm khỏi giỏ hàng");
    }
}
