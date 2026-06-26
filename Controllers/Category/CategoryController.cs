using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers.Category;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? IconUrl { get; set; }
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public List<CategoryDto> SubCategories { get; set; } = new();
}

[Route("api/[controller]")]
[ApiController]
public class CategoryController(ApplicationDbContext context, BE_ECOMMERCE.Services.CloudinaryService cloudinaryService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly BE_ECOMMERCE.Services.CloudinaryService _cloudinaryService = cloudinaryService;

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        // Lấy toàn bộ danh mục từ database để tự build cây (nhanh hơn nhiều so với Include đệ quy)
        var allCategories = await _context.Categories.ToListAsync();

        // Nhóm các danh mục theo ParentId
        var lookup = allCategories.ToLookup(c => c.ParentId);

        // Hàm đệ quy để map từ Entity sang DTO
        CategoryDto MapToDto(BE_ECOMMERCE.Entities.Category.Category c)
        {
            return new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                IconUrl = c.IconUrl,
                ParentId = c.ParentId,
                Level = c.Level,
                SubCategories = lookup[c.Id].Select(MapToDto).ToList()
            };
        }

        // Lấy danh mục gốc (ParentId == null) và đệ quy build cây
        var rootCategories = lookup[null].Select(MapToDto).ToList();

        return Ok(rootCategories);
    }

    [HttpPost("admin")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDto model)
    {
        int level = 0;
        if (model.ParentId.HasValue)
        {
            var parent = await _context.Categories.FindAsync(model.ParentId.Value);
            if (parent == null) return BadRequest("Danh mục cha không tồn tại.");
            level = parent.Level + 1;
        }

        var newCategory = new BE_ECOMMERCE.Entities.Category.Category
        {
            Name = model.Name,
            IconUrl = model.IconUrl,
            ParentId = model.ParentId,
            Level = level,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        try
        {
            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();
            return Ok(newCategory);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(newCategory.IconUrl))
            {
                await _cloudinaryService.DeleteImageAsync(newCategory.IconUrl, "images/categories");
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi lưu danh mục." });
        }
    }

    [HttpPut("admin/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto model)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound("Danh mục không tồn tại.");

        int level = 0;
        if (model.ParentId.HasValue)
        {
            if (model.ParentId.Value == id) return BadRequest("Không thể chọn danh mục cha là chính nó.");
            var parent = await _context.Categories.FindAsync(model.ParentId.Value);
            if (parent == null) return BadRequest("Danh mục cha không tồn tại.");
            level = parent.Level + 1;
        }

        string? oldImageUrl = category.IconUrl;
        bool isImageChanged = oldImageUrl != model.IconUrl;

        category.Name = model.Name;
        category.IconUrl = model.IconUrl;
        category.ParentId = model.ParentId;
        category.Level = level;
        category.UpdatedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync();

            if (isImageChanged && !string.IsNullOrEmpty(oldImageUrl))
            {
                await _cloudinaryService.DeleteImageAsync(oldImageUrl, "images/categories");
            }

            return Ok(category);
        }
        catch (Exception ex)
        {
            if (isImageChanged && !string.IsNullOrEmpty(model.IconUrl))
            {
                await _cloudinaryService.DeleteImageAsync(model.IconUrl, "images/categories");
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật danh mục." });
        }
    }

    [HttpDelete("admin/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories
            .Include(c => c.SubCategories)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null) return NotFound("Danh mục không tồn tại.");

        if (category.SubCategories.Any())
        {
            return BadRequest("Không thể xóa danh mục này vì vẫn còn danh mục con.");
        }

        if (category.Products.Any())
        {
            return BadRequest("Không thể xóa danh mục này vì vẫn còn sản phẩm thuộc danh mục.");
        }

        string? iconUrlToDelete = category.IconUrl;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(iconUrlToDelete))
        {
            try
            {
                await _cloudinaryService.DeleteImageAsync(iconUrlToDelete, "images/categories");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xóa ảnh danh mục: {ex.Message}");
            }
        }

        return Ok(new { message = "Xóa danh mục thành công." });
    }
}
