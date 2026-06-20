using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Constants; // Gọi mảng Hằng số của bạn vào
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using BE_ECOMMERCE.Services.VnPay;
using Microsoft.EntityFrameworkCore;
using BE_ECOMMERCE.Services.Email;
using BE_ECOMMERCE.Services.Notification;

namespace BE_ECOMMERCE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;

        public PaymentController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService, INotificationService notificationService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        // Dữ liệu từ ReactJS gửi lên
        public class PaymentCreateRequest
        {
            public int InternalOrderId { get; set; } // Mã đơn hàng trong Database của mình
        }

        [HttpPost("vnpay-create")]
        [Authorize]
        public async Task<IActionResult> CreateVnPayPayment([FromBody] PaymentCreateRequest request)
        {
            var order = await _context.Orders.FindAsync(request.InternalOrderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status != OrderStatus.PendingPayment)
                return BadRequest("Order is not in pending payment state");

            string vnp_Returnurl = _configuration["VNPAY:vnp_ReturnUrl"];
            string vnp_Url = _configuration["VNPAY:vnp_Url"];
            string vnp_TmnCode = _configuration["VNPAY:vnp_TmnCode"];
            string vnp_HashSecret = _configuration["VNPAY:vnp_HashSecret"];

            VnPayLibrary vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", ((long)(order.TotalAmount * 100)).ToString()); // Nhân 100 vì vnpay quy định
            
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(2).ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", VnPayLibrary.GetIpAddress(HttpContext));
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "ThanhToanDonHang" + order.Id);
            vnpay.AddRequestData("vnp_OrderType", "other"); // default
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", order.Id.ToString() + "_" + DateTime.Now.Ticks.ToString()); // Mã tham chiếu duy nhất

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

            return Ok(new { url = paymentUrl });
        }

        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn()
        {
            if (Request.Query.Count > 0)
            {
                string vnp_HashSecret = _configuration["VNPAY:vnp_HashSecret"];
                var vnpayData = Request.Query;
                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (string s in vnpayData.Keys)
                {
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(s, vnpayData[s]);
                    }
                }

                long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef").Split('_')[0]);
                long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_SecureHash = Request.Query["vnp_SecureHash"];
                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        var order = await _context.Orders.FindAsync((int)orderId);
                        if (order != null && order.Status == OrderStatus.PendingPayment)
                        {
                            order.Status = OrderStatus.Pending;
                            order.IsPaid = true;
                            order.TransactionId = vnpayTranId.ToString();
                            order.VnPayTxnRef = vnpay.GetResponseData("vnp_TxnRef");
                            order.VnPayPayDate = vnpay.GetResponseData("vnp_PayDate");

                            // Cộng ví Admin
                            var adminWallet = await _context.SandboxWallets.FirstOrDefaultAsync(w => w.AccountType == "ADMIN");
                            if (adminWallet != null)
                            {
                                adminWallet.Balance += order.TotalAmount;
                                
                                var transaction = new BE_ECOMMERCE.Entities.System.TransactionHistory
                                {
                                    WalletId = adminWallet.Id,
                                    OrderId = order.Id,
                                    AmountChanged = order.TotalAmount,
                                    NewBalance = adminWallet.Balance,
                                    TransactionType = "PAYMENT",
                                    TransactionDate = DateTime.Now,
                                    Description = $"Nhận thanh toán VNPay từ đơn hàng #{order.Id}"
                                };
                                _context.TransactionHistories.Add(transaction);
                            }

                            await _context.SaveChangesAsync();
                            
                            // Send Email
                            string emailStatus = "Not attempted";
                            // Cần Include OrderItems và ProductVariant để hiển thị trong email
                            var orderWithItems = await _context.Orders
                                .Include(o => o.OrderItems)
                                .ThenInclude(oi => oi.ProductVariant)
                                .ThenInclude(v => v.Product)
                                .FirstOrDefaultAsync(o => o.Id == order.Id);

                            var user = await _context.Users.FindAsync(order.UserId);
                            if (orderWithItems != null && user != null)
                            {
                                emailStatus = await _emailService.SendOrderConfirmationEmailAsync(orderWithItems, user);
                            }

                            // Gửi thông báo
                            await _notificationService.SendNotificationAsync(
                                order.UserId,
                                "Thanh toán VNPay thành công",
                                $"Đơn hàng #{order.Id} đã được thanh toán thành công qua VNPay.",
                                "OrderCreated",
                                order.Id.ToString()
                            );

                            return Ok(new { success = true, message = "Thanh toán thành công", emailStatus = emailStatus });
                        }
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Thanh toán thất bại hoặc đã bị hủy" });
                    }
                }
                else
                {
                    return BadRequest(new { success = false, message = "Chữ ký không hợp lệ" });
                }
            }
            return BadRequest(new { success = false, message = "Không có dữ liệu trả về" });
        }
    }
}

