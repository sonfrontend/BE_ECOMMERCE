using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Promotion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Controllers.Promotion;

[Route("api/[controller]")]
[ApiController]
public class PromotionController(ApplicationDbContext context, BE_ECOMMERCE.Services.CloudinaryService cloudinaryService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly BE_ECOMMERCE.Services.CloudinaryService _cloudinaryService = cloudinaryService;

    // Lấy các Promotion ĐANG HOẠT ĐỘNG (Dành cho trang chủ)
    [HttpGet("active")]
    public async Task<IActionResult> GetActivePromotions()
    {
        var today = DateTime.UtcNow;
        var activePromotions = await _context.Promotions
            .Where(p => p.IsActived && p.StartDate <= today && p.EndDate >= today)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            string userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
            {
                bool hasOrdered = await _context.Orders.AnyAsync(o => o.UserId == userId);
                if (hasOrdered)
                {
                    return Ok(new List<BE_ECOMMERCE.Entities.Promotion.Promotion>());
                }
            }
        }

        return Ok(activePromotions);
    }

    // Lấy tất cả Promotion (Dành cho Admin)
    [HttpGet]
    public async Task<IActionResult> GetAllPromotions()
    {
        var promotions = await _context.Promotions.OrderByDescending(p => p.Id).ToListAsync();
        return Ok(promotions);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion([FromBody] BE_ECOMMERCE.Entities.Promotion.Promotion promotion)
    {
        _context.Promotions.Add(promotion);
        try
        {
            await _context.SaveChangesAsync();
            return Ok(promotion);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(promotion.ImageUrl))
            {
                await _cloudinaryService.DeleteBannerImageAsync(promotion.ImageUrl);
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi tạo khuyến mãi." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] BE_ECOMMERCE.Entities.Promotion.Promotion promotion)
    {
        var existing = await _context.Promotions.FindAsync(id);
        if (existing == null) return NotFound("Không tìm thấy khuyến mãi");

        string? oldImageUrl = existing.ImageUrl;
        bool isImageChanged = !string.IsNullOrEmpty(oldImageUrl) && oldImageUrl != promotion.ImageUrl;

        existing.Title = promotion.Title;
        existing.Description = promotion.Description;
        existing.ImageUrl = promotion.ImageUrl;
        existing.Link = promotion.Link;
        existing.DiscountPercentage = promotion.DiscountPercentage;
        existing.StartDate = promotion.StartDate;
        existing.EndDate = promotion.EndDate;
        existing.IsActived = promotion.IsActived;

        try
        {
            await _context.SaveChangesAsync();

            if (isImageChanged)
            {
                try
                {
                    await _cloudinaryService.DeleteBannerImageAsync(oldImageUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi xóa ảnh cũ Cloudinary: {ex.Message}");
                }
            }

            return Ok(existing);
        }
        catch (Exception ex)
        {
            if (isImageChanged && !string.IsNullOrEmpty(promotion.ImageUrl))
            {
                await _cloudinaryService.DeleteBannerImageAsync(promotion.ImageUrl);
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật khuyến mãi." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return NotFound("Không tìm thấy khuyến mãi");

        string? imageUrlToDelete = promotion.ImageUrl;

        _context.Promotions.Remove(promotion);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(imageUrlToDelete))
        {
            try
            {
                await _cloudinaryService.DeleteBannerImageAsync(imageUrlToDelete);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xóa ảnh cũ Cloudinary: {ex.Message}");
            }
        }

        return Ok(new { message = "Xóa khuyến mãi thành công" });
    }
}
