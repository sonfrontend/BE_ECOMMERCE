using System.Threading.Tasks;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Entities.Auth;

namespace BE_ECOMMERCE.Services.Email
{
    public interface IEmailService
    {
        Task<string> SendOrderConfirmationEmailAsync(Order order, User user);
    }
}
