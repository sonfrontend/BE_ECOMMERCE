using BE_ECOMMERCE.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Constants;

namespace BE_ECOMMERCE.Services
{
    public class OrderCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupService> _logger;

        public OrderCleanupService(IServiceProvider serviceProvider, ILogger<OrderCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        
                        // Tìm các đơn hàng PendingPayment quá 2 phút
                        var expiredTime = DateTime.Now.AddMinutes(-2);
                        
                        var expiredOrders = await context.Orders
                            .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                            .Where(o => o.Status == OrderStatus.PendingPayment && o.OrderDate <= expiredTime)
                            .ToListAsync(stoppingToken);

                        if (expiredOrders.Any())
                        {
                            foreach (var order in expiredOrders)
                            {
                                order.Status = OrderStatus.Cancelled;
                                foreach (var item in order.OrderItems)
                                {
                                    if (item.ProductVariant != null)
                                    {
                                        item.ProductVariant.StockQuantity += item.Quantity;
                                        item.ProductVariant.SoldQuantity -= item.Quantity;
                                        if (item.ProductVariant.Product != null)
                                        {
                                            item.ProductVariant.Product.SoldQuantity -= item.Quantity;
                                        }
                                    }
                                }
                                _logger.LogInformation($"Auto-cancelled order {order.Id} due to payment timeout and restored stock.");
                            }
                            
                            await context.SaveChangesAsync(stoppingToken);
                        }

                        // Tự động chốt đơn (Auto-Complete) nếu trạng thái là Delivered quá 3 ngày
                        var autocompleteTime = DateTime.Now.AddDays(-3);
                        var deliveredOrdersToComplete = await context.Orders
                            .Where(o => o.Status == OrderStatus.Delivered && o.DeliveredDate != null && o.DeliveredDate <= autocompleteTime)
                            .ToListAsync(stoppingToken);

                        if (deliveredOrdersToComplete.Any())
                        {
                            foreach (var order in deliveredOrdersToComplete)
                            {
                                order.Status = OrderStatus.Completed;
                                order.IsPaid = true;
                                _logger.LogInformation($"Auto-completed order {order.Id} after 3 days of delivery.");
                            }

                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OrderCleanupService.");
                }

                // Chờ 10 giây trước khi kiểm tra lại
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}

