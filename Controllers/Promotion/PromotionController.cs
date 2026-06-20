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
public class PromotionController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    // Lấy các Promotion ĐANG HOẠT ĐỘNG (Dành cho trang chủ)
    [HttpGet("active")]
    public async Task<IActionResult> GetActivePromotions()
    {
        var today = DateTime.UtcNow;
        var activePromotions = await _context.Promotions
            .Where(p => p.IsActived && p.StartDate <= today && p.EndDate >= today)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

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
        await _context.SaveChangesAsync();
        return Ok(promotion);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] BE_ECOMMERCE.Entities.Promotion.Promotion promotion)
    {
        var existing = await _context.Promotions.FindAsync(id);
        if (existing == null) return NotFound("Không tìm thấy khuyến mãi");

        existing.Title = promotion.Title;
        existing.Description = promotion.Description;
        existing.ImageUrl = promotion.ImageUrl;
        existing.Link = promotion.Link;
        existing.DiscountPercentage = promotion.DiscountPercentage;
        existing.StartDate = promotion.StartDate;
        existing.EndDate = promotion.EndDate;
        existing.IsActived = promotion.IsActived;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return NotFound("Không tìm thấy khuyến mãi");

        _context.Promotions.Remove(promotion);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Xóa khuyến mãi thành công" });
    }
}
