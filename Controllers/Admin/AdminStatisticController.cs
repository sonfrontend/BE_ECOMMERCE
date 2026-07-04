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
    public async Task<IActionResult> GetRevenueStats([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? year)
    {
        try
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                var ordersInRange = await _context.Orders
                    .Where(o => o.OrderDate.Date >= startDate.Value.Date && o.OrderDate.Date <= endDate.Value.Date && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Completed))
                    .ToListAsync();
                
                var totalDays = (endDate.Value.Date - startDate.Value.Date).Days + 1;
                if (totalDays <= 31)
                {
                    var dailyRevenue = Enumerable.Range(0, totalDays).Select(offset =>
                    {
                        var currentDay = startDate.Value.Date.AddDays(offset);
                        return new
                        {
                            Month = currentDay.ToString("dd/MM"),
                            Revenue = ordersInRange.Where(o => o.OrderDate.Date == currentDay).Sum(o => o.TotalAmount)
                        };
                    }).ToList();

                    return Ok(new
                    {
                        Data = dailyRevenue,
                        TotalRevenue = ordersInRange.Sum(o => o.TotalAmount)
                    });
                }
                else
                {
                    var monthlyData = ordersInRange.GroupBy(o => new { o.OrderDate.Month, o.OrderDate.Year })
                        .Select(g => new
                        {
                            Month = $"Tháng {g.Key.Month}/{g.Key.Year}",
                            Revenue = g.Sum(o => o.TotalAmount)
                        }).ToList();

                    return Ok(new
                    {
                        Data = monthlyData,
                        TotalRevenue = ordersInRange.Sum(o => o.TotalAmount)
                    });
                }
            }

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
    public async Task<IActionResult> GetOrderStatusStats([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? month, [FromQuery] int? year)
    {
        try
        {
            var query = _context.Orders.AsQueryable();
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date >= startDate.Value.Date && o.OrderDate.Date <= endDate.Value.Date);
            }
            else
            {
                var targetYear = year ?? DateTime.Now.Year;
                query = query.Where(o => o.OrderDate.Year == targetYear);

                if (month.HasValue)
                {
                    query = query.Where(o => o.OrderDate.Month == month.Value);
                }
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
    public async Task<IActionResult> GetDashboardSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var userQuery = _context.Users.AsQueryable();
            var productQuery = _context.Products.AsQueryable();
            var orderQuery = _context.Orders.AsQueryable();

            if (startDate.HasValue && endDate.HasValue)
            {
                orderQuery = orderQuery.Where(o => o.OrderDate.Date >= startDate.Value.Date && o.OrderDate.Date <= endDate.Value.Date);
            }
            var totalUsers = await userQuery.CountAsync();
            var totalProducts = await productQuery.CountAsync();
            var totalOrders = await orderQuery.CountAsync();
            
            // Doanh thu chỉ tính đơn hàng đã Completed (hoặc Delivered)
            var totalRevenue = await orderQuery
                .Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.TotalAmount);

            // Số sản phẩm đã bán: tính từ OrderItems của các đơn Completed
            var totalProductsSold = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => (oi.Order.Status == OrderStatus.Completed || oi.Order.Status == OrderStatus.Delivered) && (!startDate.HasValue || oi.Order.OrderDate.Date >= startDate.Value.Date) && (!endDate.HasValue || oi.Order.OrderDate.Date <= endDate.Value.Date))
                .SumAsync(oi => oi.Quantity);

            // Số sản phẩm thất lạc (Lost)
            var totalProductsLost = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.Status == OrderStatus.Lost && (!startDate.HasValue || oi.Order.OrderDate.Date >= startDate.Value.Date) && (!endDate.HasValue || oi.Order.OrderDate.Date <= endDate.Value.Date))
                .SumAsync(oi => oi.Quantity);

            var topSellingCategoryObj = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant).ThenInclude(pv => pv.Product).ThenInclude(p => p.Categories)
                .Where(oi => (!startDate.HasValue || oi.Order.OrderDate.Date >= startDate.Value.Date) && (!endDate.HasValue || oi.Order.OrderDate.Date <= endDate.Value.Date))
                .Where(oi => oi.ProductVariant.Product.CategoryId != null && oi.Order.Status != OrderStatus.Cancelled)
                .GroupBy(oi => oi.ProductVariant.Product.Categories.Name)
                .Select(g => new { CategoryName = g.Key, TotalSold = g.Sum(oi => oi.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .FirstOrDefaultAsync();
            var topSellingCategory = topSellingCategoryObj != null ? topSellingCategoryObj.CategoryName : "Chưa có dữ liệu";

            // Sản phẩm được mua nhiều nhất
            var topSellingProductObj = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant).ThenInclude(pv => pv.Product)
                .Where(oi => (!startDate.HasValue || oi.Order.OrderDate.Date >= startDate.Value.Date) && (!endDate.HasValue || oi.Order.OrderDate.Date <= endDate.Value.Date))
                .Where(oi => oi.Order.Status != OrderStatus.Cancelled)
                .GroupBy(oi => new { oi.ProductVariant.Product.ProductName, oi.ProductVariant.Product.ImageUrl })
                .Select(g => new { ProductName = g.Key.ProductName, ImageUrl = g.Key.ImageUrl, TotalSold = g.Sum(oi => oi.Quantity) })
                .OrderByDescending(x => x.TotalSold)
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

    [HttpGet("lost-products")]
    public async Task<IActionResult> GetLostProducts([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var query = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Where(oi => oi.Order.Status == OrderStatus.Lost);

            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(oi => oi.Order.OrderDate.Date >= startDate.Value.Date && oi.Order.OrderDate.Date <= endDate.Value.Date);
            }

            var lostProducts = await query
                .Select(oi => new
                {
                    orderId = oi.OrderId,
                    orderDate = oi.Order.OrderDate,
                    productName = oi.ProductVariant.Product.ProductName + " - " + oi.ProductVariant.Color + " (" + oi.ProductVariant.Size + ")",
                    quantity = oi.Quantity,
                    price = oi.UnitPrice,
                    total = oi.Quantity * oi.UnitPrice
                })
                .ToListAsync();

            return Ok(lostProducts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
        }
    }
}
