using BE_ECOMMERCE.Data;
using ProductEntity = BE_ECOMMERCE.Entities.Product.Product;
using VariantEntity = BE_ECOMMERCE.Entities.Product.ProductVariant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BE_ECOMMERCE.Controllers.Admin;

public class AdminVariantDto
{
    public int VariantId { get; set; }
    public string SKU { get; set; }
    public string Color { get; set; }
    public string Size { get; set; }
    public int StockQuantity { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public string ImageUrl { get; set; }
}

public class AdminProductDto
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    public List<AdminVariantDto> Variants { get; set; } = new();
}

[Route("api/[controller]")]
[ApiController]
public class AdminProductController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductVariants)
                .AsQueryable();
                
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    articleId = p.ProductId,
                    productName = p.ProductName,
                    categoryId = p.CategoryId,
                    categoryName = p.Categories != null ? p.Categories.Name : null,
                    description = p.Description,
                    imageUrl = p.ImageUrl,
                    variants = _context.ProductVariants.Where(v => v.ProductId == p.ProductId).Select(v => new
                    {
                        variantId = v.VariantId,
                        sku = v.SKU,
                        color = v.Color,
                        size = v.Size,
                        stockQuantity = v.StockQuantity,
                        originalPrice = v.OriginalPrice,
                        currentPrice = v.CurrentPrice,
                        imageUrl = v.ImageUrl
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                TotalItems = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = items
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] AdminProductDto request)
    {
        if (string.IsNullOrEmpty(request.ProductId))
            return BadRequest("ProductId is required");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var product = new ProductEntity
            {
                ProductId = request.ProductId,
                ProductName = request.ProductName,
                CategoryId = request.CategoryId,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                CreatedAt = DateTime.UtcNow,
                IsActived = true
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (request.Variants != null && request.Variants.Any())
            {
                var variants = request.Variants.Select(v => new VariantEntity
                {
                    ProductId = product.ProductId,
                    SKU = v.SKU,
                    Color = v.Color,
                    Size = v.Size,
                    StockQuantity = v.StockQuantity,
                    OriginalPrice = v.OriginalPrice,
                    CurrentPrice = v.CurrentPrice,
                    ImageUrl = v.ImageUrl,
                    CreatedAt = DateTime.UtcNow,
                    IsActived = true
                });
                _context.ProductVariants.AddRange(variants);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return Ok(new { message = "Thêm sản phẩm thành công" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpPut("{articleId}")]
    public async Task<IActionResult> UpdateProduct(string articleId, [FromBody] AdminProductDto request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == articleId);

            if (product == null) return NotFound("Sản phẩm không tồn tại");

            product.ProductName = request.ProductName;
            product.CategoryId = request.CategoryId;
            product.Description = request.Description;
            product.ImageUrl = request.ImageUrl;
            product.UpdatedAt = DateTime.UtcNow;

            if (request.Variants != null)
            {
                var existingVariants = await _context.ProductVariants.Where(v => v.ProductId == product.ProductId).ToListAsync();

                // Xóa các variant không còn tồn tại
                var incomingVariantIds = request.Variants.Where(v => v.VariantId > 0).Select(v => v.VariantId).ToList();
                var variantsToRemove = existingVariants.Where(v => !incomingVariantIds.Contains(v.VariantId)).ToList();
                _context.ProductVariants.RemoveRange(variantsToRemove);

                // Cập nhật và thêm mới
                foreach (var v in request.Variants)
                {
                    if (v.VariantId > 0)
                    {
                        var existing = existingVariants.FirstOrDefault(pv => pv.VariantId == v.VariantId);
                        if (existing != null)
                        {
                            existing.SKU = v.SKU;
                            existing.Color = v.Color;
                            existing.Size = v.Size;
                            existing.StockQuantity = v.StockQuantity;
                            existing.OriginalPrice = v.OriginalPrice;
                            existing.CurrentPrice = v.CurrentPrice;
                            existing.ImageUrl = v.ImageUrl;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        _context.ProductVariants.Add(new VariantEntity
                        {
                            ProductId = product.ProductId,
                            SKU = v.SKU,
                            Color = v.Color,
                            Size = v.Size,
                            StockQuantity = v.StockQuantity,
                            OriginalPrice = v.OriginalPrice,
                            CurrentPrice = v.CurrentPrice,
                            ImageUrl = v.ImageUrl,
                            CreatedAt = DateTime.UtcNow,
                            IsActived = true
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { message = "Cập nhật thành công" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpDelete("{articleId}")]
    public async Task<IActionResult> DeleteProduct(string articleId)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == articleId);

        if (product == null) return NotFound("Sản phẩm không tồn tại");

        var variants = await _context.ProductVariants.Where(v => v.ProductId == product.ProductId).ToListAsync();
        _context.ProductVariants.RemoveRange(variants);
        _context.Products.Remove(product);
        
        await _context.SaveChangesAsync();
        return Ok(new { message = "Xóa sản phẩm thành công" });
    }
}
