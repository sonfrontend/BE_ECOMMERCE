using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BE_ECOMMERCE.Controllers.Admin;

[Route("api/[controller]")]
[ApiController]
// [Authorize(Roles = "Admin")] // Uncomment this if you have Admin role setup
public class AdminStatisticController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueStats([FromQuery] int? year)
    {
        try
        {
            var targetYear = year ?? DateTime.Now.Year;

            // Lấy tất cả đơn hàng đã giao thành công hoặc hoàn thành trong năm
            var orders = await _context.Orders
                .Where(o => o.OrderDate.Year == targetYear && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Completed))
                .ToListAsync();

            // Tính tổng doanh thu theo từng tháng
            var monthlyRevenue = Enumerable.Range(1, 12).Select(month => new
            {
                Month = $"Tháng {month}",
                Revenue = orders.Where(o => o.OrderDate.Month == month).Sum(o => o.TotalAmount)
            }).ToList();

            return Ok(new
            {
                Year = targetYear,
                Data = monthlyRevenue,
                TotalRevenue = orders.Sum(o => o.TotalAmount)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet("order-status")]
    public async Task<IActionResult> GetOrderStatusStats([FromQuery] int? month, [FromQuery] int? year)
    {
        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var query = _context.Orders.Where(o => o.OrderDate.Year == targetYear);

            if (month.HasValue)
            {
                query = query.Where(o => o.OrderDate.Month == month.Value);
            }

            // Theo yêu cầu: đã bán (Completed), đã hủy (Cancelled), chờ xử lý (Pending), chưa thanh toán (PendingPayment)
            var stats = new List<object>
            {
                new { Status = "Đã bán", Count = await query.CountAsync(o => o.Status == OrderStatus.Completed) },
                new { Status = "Đã hủy", Count = await query.CountAsync(o => o.Status == OrderStatus.Cancelled) },
                new { Status = "Đang chờ xử lý", Count = await query.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing) },
                new { Status = "Chưa thanh toán", Count = await query.CountAsync(o => o.Status == OrderStatus.PendingPayment) }
            };

            return Ok(new
            {
                Data = stats,
                TotalOrders = await query.CountAsync()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();
            
            // Doanh thu chỉ tính đơn hàng đã Completed (hoặc Delivered)
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.TotalAmount);

            // Số sản phẩm đã bán: tính từ OrderItems của các đơn Completed
            var totalProductsSold = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.Status == OrderStatus.Completed || oi.Order.Status == OrderStatus.Delivered)
                .SumAsync(oi => oi.Quantity);

            // Số sản phẩm thất lạc (Lost)
            var totalProductsLost = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.Status == OrderStatus.Lost)
                .SumAsync(oi => oi.Quantity);

            var topSellingCategoryObj = await _context.Products
                .Include(p => p.Categories)
                .Where(p => p.SoldQuantity > 0 && p.CategoryId != null)
                .GroupBy(p => p.Categories.Name)
                .Select(g => new { CategoryName = g.Key, TotalSold = g.Sum(p => p.SoldQuantity) })
                .OrderByDescending(x => x.TotalSold)
                .FirstOrDefaultAsync();
            var topSellingCategory = topSellingCategoryObj != null ? topSellingCategoryObj.CategoryName : "Chưa có dữ liệu";

            // Sản phẩm được mua nhiều nhất
            var topSellingProductObj = await _context.Products
                .OrderByDescending(p => p.SoldQuantity)
                .FirstOrDefaultAsync();
            var topSellingProduct = topSellingProductObj != null ? topSellingProductObj.ProductName : "Chưa có dữ liệu";
            var topSellingProductImageUrl = topSellingProductObj != null ? topSellingProductObj.ImageUrl : null;

            return Ok(new
            {
                TotalUsers = totalUsers,
                TotalProducts = totalProducts,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                TotalProductsSold = totalProductsSold,
                TotalProductsLost = totalProductsLost,
                TopSellingCategory = topSellingCategory,
                TopSellingProduct = topSellingProduct,
                TopSellingProductImageUrl = topSellingProductImageUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts()
    {
        try
        {
            var lowStockVariants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.StockQuantity <= 10)
                .OrderBy(v => v.StockQuantity)
                .Take(5)
                .Select(v => new
                {
                    productId = v.ProductId,
                    productName = v.Product.ProductName + " - " + v.Color + " (" + v.Size + ")",
                    stockQuantity = v.StockQuantity,
                    soldQuantity = v.SoldQuantity,
                    originalPrice = v.OriginalPrice
                })
                .ToListAsync();

            return Ok(lowStockVariants);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }
}
