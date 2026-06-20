using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Constants;
using BE_ECOMMERCE.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BE_ECOMMERCE.Services;

public class AutoResolveDisputeService(IServiceProvider serviceProvider, ILogger<AutoResolveDisputeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AutoResolveDisputeService is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var executor = scope.ServiceProvider.GetRequiredService<IDisputeResolutionExecutor>();

                // Tìm các khiếu nại đang PendingResolution quá 3 ngày chưa chốt
                var threeDaysAgo = DateTime.Now.AddDays(-3);
                
                var expiredComplaints = await dbContext.Complaints
                    .Include(c => c.Order)
                        .ThenInclude(o => o.OrderItems)
                            .ThenInclude(oi => oi.ProductVariant)
                                .ThenInclude(pv => pv.Product)
                    .Include(c => c.ResolutionTemplate)
                    .Where(c => c.Status == "PendingResolution" && c.UpdatedAt != null && c.UpdatedAt < threeDaysAgo)
                    .ToListAsync(stoppingToken);

                if (expiredComplaints.Count > 0)
                {
                    foreach (var complaint in expiredComplaints)
                    {
                        var result = await executor.ExecuteResolutionAsync(complaint, "Auto-System");
                        if (result.Success)
                        {
                            logger.LogInformation("Auto-resolved complaint {ComplaintId}", complaint.Id);
                        }
                        else
                        {
                            logger.LogError("Failed to auto-resolve complaint {ComplaintId}: {Message}", complaint.Id, result.Message);
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred executing AutoResolveDisputeService.");
            }

            // Kiểm tra mỗi 1 giờ
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

