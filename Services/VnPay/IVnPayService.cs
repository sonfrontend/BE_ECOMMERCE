using System.Threading.Tasks;
using BE_ECOMMERCE.Entities.Order;

namespace BE_ECOMMERCE.Services.VnPay
{
    public interface IVnPayService
    {
        Task<bool> RefundAsync(Order order, decimal amount, string transactionType, string createBy);
    }
}
