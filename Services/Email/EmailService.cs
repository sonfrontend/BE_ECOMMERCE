using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Entities.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BE_ECOMMERCE.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SendOrderConfirmationEmailAsync(Order order, User user)
        {
            try
            {
                var smtpConfig = _configuration.GetSection("SmtpConfig");
                var host = smtpConfig["Host"];
                var portStr = smtpConfig["Port"];
                var enableSslStr = smtpConfig["EnableSsl"];
                var username = smtpConfig["Username"];
                var password = smtpConfig["Password"];

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || username == "your_email@gmail.com")
                {
                    _logger.LogWarning("SMTP Configuration is missing or using default placeholder. Email not sent.");
                    return "SMTP configuration missing in appsettings.json";
                }

                int.TryParse(portStr, out int port);
                bool.TryParse(enableSslStr, out bool enableSsl);

                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(username, password);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(username, "Cửa Hàng Quần Áo - ECommerce"),
                        Subject = $"Xác nhận đơn hàng #{order.Id} đặt thành công",
                        IsBodyHtml = true,
                        BodyEncoding = Encoding.UTF8
                    };

                    var recipientEmail = !string.IsNullOrEmpty(order.Email) ? order.Email : user?.Email;
                    if (!string.IsNullOrEmpty(recipientEmail))
                    {
                        mailMessage.To.Add(recipientEmail);
                    }
                    else
                    {
                        // Nếu user không có email, lấy email admin làm To thay vì CC
                        mailMessage.To.Add(username);
                    }

                    // Gửi một bản sao (CC) cho chính admin để chủ shop nhận được thông báo đơn hàng mới
                    if (!string.IsNullOrEmpty(recipientEmail))
                    {
                        mailMessage.CC.Add(username);
                    }

                    // Build HTML Body
                    string recipientName = !string.IsNullOrEmpty(order.RecipientName) ? order.RecipientName : (user.FullName ?? "Quý khách");
                    var bodyBuilder = new StringBuilder();
                    bodyBuilder.Append($"<h2>Xin chào {recipientName}.</h2>");
                    bodyBuilder.Append($"<p>Cảm ơn bạn đã đặt hàng. Đơn hàng <strong>#{order.Id}</strong> của bạn đã được tạo thành công.</p>");
                    bodyBuilder.Append("<h3>Chi tiết đơn hàng:</h3>");
                    bodyBuilder.Append("<table border='1' cellpadding='10' cellspacing='0' style='border-collapse: collapse;'>");
                    bodyBuilder.Append("<tr><th>Hình ảnh</th><th>Sản phẩm</th><th>Phân loại</th><th>Số lượng</th><th>Đơn giá</th></tr>");

                    foreach (var item in order.OrderItems)
                    {
                        string imgUrl = item.ProductVariant?.ImageUrl ?? item.ProductVariant?.Product?.ImageUrl ?? "https://via.placeholder.com/50";
                        bodyBuilder.Append("<tr>");
                        bodyBuilder.Append($"<td style='text-align: center;'><img src='{imgUrl}' alt='product' width='50' height='50' style='object-fit: cover;'/></td>");
                        bodyBuilder.Append($"<td>{item.ProductVariant?.Product?.ProductName ?? "Sản phẩm"}</td>");
                        bodyBuilder.Append($"<td>{item.ProductVariant?.Color} - {item.ProductVariant?.Size}</td>");
                        bodyBuilder.Append($"<td>{item.Quantity}</td>");
                        bodyBuilder.Append($"<td>{item.UnitPrice:N0}đ</td>");
                        bodyBuilder.Append("</tr>");
                    }
                    bodyBuilder.Append("</table>");

                    if (!string.IsNullOrEmpty(order.VoucherCode))
                    {
                        bodyBuilder.Append($"<p><strong>Mã giảm giá áp dụng:</strong> {order.VoucherCode}</p>");
                    }
                    if (order.DiscountAmount > 0)
                    {
                        bodyBuilder.Append($"<p><strong>Số tiền được giảm:</strong> -{order.DiscountAmount:N0}đ</p>");
                    }

                    bodyBuilder.Append($"<p><strong>Tổng tiền thanh toán:</strong> <span style='color:red; font-weight:bold;'>{order.TotalAmount:N0}đ</span></p>");
                    bodyBuilder.Append($"<p><strong>Địa chỉ nhận hàng:</strong> {order.ShippingAddress}</p>");

                    // Phương thức và trạng thái thanh toán
                    string paymentMethodText = order.PaymentMethod == "VNPAY" ? "Thanh toán qua VNPay" : "Thanh toán khi nhận hàng (COD)";
                    string paymentStatusText = order.IsPaid ? "<span style='color:green;'>Đã thanh toán</span>" : "<span style='color:red;'>Chưa thanh toán</span>";

                    bodyBuilder.Append($"<p><strong>Phương thức thanh toán:</strong> {paymentMethodText}</p>");
                    bodyBuilder.Append($"<p><strong>Trạng thái thanh toán:</strong> {paymentStatusText}</p>");

                    bodyBuilder.Append("<br/><p>Trân trọng,</p>");
                    bodyBuilder.Append("<p><strong>Đội ngũ ECommerce</strong></p>");

                    mailMessage.Body = bodyBuilder.ToString();

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Sent confirmation email to {user.Email} for order #{order.Id}");
                    return "OK";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email for order #{order.Id} to {user?.Email}");
                return ex.Message;
            }
        }
    }
}
