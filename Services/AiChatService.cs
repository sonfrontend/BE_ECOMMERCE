using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BE_ECOMMERCE.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BE_ECOMMERCE.Services;

public class AiChatService
{
    private readonly ApplicationDbContext _context;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public AiChatService(ApplicationDbContext context, IConfiguration config, HttpClient httpClient)
    {
        _context = context;
        _apiKey = config["GeminiApiKey"];
        _httpClient = httpClient;
    }

    public async Task<string> AskAiAsync(Guid userId, string userMessage, string? sharedProductId = null, string? imageName = null, System.Collections.Generic.List<BE_ECOMMERCE.Controllers.AiChatController.ChatMessageHistory>? history = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return "Hệ thống AI chưa được cấu hình API Key. Vui lòng liên hệ Admin.";
        }

        // 1. Thu thập Context từ Database
        var contextBuilder = new StringBuilder();

        // 1.1 Lấy thông tin đơn hàng gần nhất
        var recentOrders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Take(3)
            .Select(o => new { o.Id, o.Status, o.TotalAmount, o.OrderDate })
            .ToListAsync();

        if (recentOrders.Any())
        {
            contextBuilder.AppendLine("THÔNG TIN ĐƠN HÀNG CỦA KHÁCH:");
            foreach (var order in recentOrders)
            {
                contextBuilder.AppendLine($"- Đơn hàng #{order.Id} đặt ngày {order.OrderDate:dd/MM/yyyy}: Trạng thái '{order.Status}', Tổng tiền: {order.TotalAmount:N0}đ");
            }
        }
        else
        {
            contextBuilder.AppendLine("THÔNG TIN ĐƠN HÀNG CỦA KHÁCH: Khách hàng hiện chưa có đơn hàng nào trên hệ thống.");
        }

        // 1.1b Lấy thông tin giỏ hàng
        var cartItems = await _context.CartItems
            .Include(c => c.ProductVariant)
            .ThenInclude(v => v.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartItems.Any())
        {
            contextBuilder.AppendLine("THÔNG TIN GIỎ HÀNG CỦA KHÁCH:");
            foreach (var item in cartItems)
            {
                var p = item.ProductVariant.Product;
                contextBuilder.AppendLine($"- Sản phẩm: {p.ProductName} (Size {item.ProductVariant.Size}, Màu {item.ProductVariant.Color}), Số lượng: {item.Quantity}, Giá: {item.ProductVariant.CurrentPrice:N0}đ");
            }
        }
        else
        {
            contextBuilder.AppendLine("THÔNG TIN GIỎ HÀNG CỦA KHÁCH: Giỏ hàng hiện đang trống.");
        }

        // 1.1c Lấy danh sách danh mục
        var categories = await _context.Categories.Where(c => c.IsActived).Select(c => new { c.Id, c.Name }).ToListAsync();
        if (categories.Any())
        {
            contextBuilder.AppendLine("DANH MỤC SẢN PHẨM CỦA SHOP:");
            foreach (var c in categories)
            {
                contextBuilder.AppendLine($"- {c.Name} (Mã: {c.Id})");
            }
        }

        // 1.1d Lấy danh sách Voucher đang hoạt động
        var activeVouchers = await _context.Vouchers.Where(v => v.IsActived && v.EndDate >= DateTime.Now).ToListAsync();
        if (activeVouchers.Any())
        {
            contextBuilder.AppendLine("\nCÁC VOUCHER/KHUYẾN MÃI ĐANG CÓ:");
            foreach (var v in activeVouchers)
            {
                contextBuilder.AppendLine($"- Mã '{v.Code}': Giảm {v.DiscountValue:N0}đ, Đơn tối thiểu: {v.MinOrderValue:N0}đ, Hạn: {v.EndDate:dd/MM/yyyy}");
            }
        }

        // 1.1e Lấy danh sách Promotion
        var activePromotions = await _context.Promotions.Where(p => p.IsActived && p.EndDate >= DateTime.Now).ToListAsync();
        if (activePromotions.Any())
        {
            contextBuilder.AppendLine("\nCÁC CHƯƠNG TRÌNH PROMOTION (KHUYẾN MÃI LỚN):");
            foreach (var p in activePromotions)
            {
                contextBuilder.AppendLine($"- {p.Title}: {p.Description} (Giảm {p.DiscountPercentage:N0}%, Hạn: {p.EndDate:dd/MM/yyyy})");
            }
        }

        // 1.2 Lấy thông tin sản phẩm (nếu khách chia sẻ hoặc nhắc đến)
        if (!string.IsNullOrEmpty(sharedProductId))
        {
            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == sharedProductId);

            if (product != null)
            {
                contextBuilder.AppendLine($"\nTHÔNG TIN SẢN PHẨM KHÁCH ĐANG XEM (ID: {product.ProductId}, Tên: {product.ProductName}):");
                contextBuilder.AppendLine($"- Giá bán: {product.ProductVariants.FirstOrDefault()?.CurrentPrice:N0}đ");
                contextBuilder.AppendLine("- Tồn kho theo phân loại:");
                foreach (var variant in product.ProductVariants)
                {
                    contextBuilder.AppendLine($"  + Size {variant.Size}, Màu {variant.Color}: {(variant.StockQuantity > 0 ? $"Còn {variant.StockQuantity} cái" : "Hết hàng")}");
                }
            }
        }
        else
        {
            // Tìm kiếm sản phẩm liên quan đến tin nhắn của người dùng
            var allProducts = await _context.Products.Select(p => new { p.ProductId, p.ProductName }).ToListAsync();

            var matchedIds = allProducts
                .Where(p => userMessage.IndexOf(p.ProductName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            p.ProductName.Split(' ').Any(w => w.Length > 3 && userMessage.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(p => p.ProductId)
                .Take(5)
                .ToList();

            if (matchedIds.Any())
            {
                var matchedProducts = await _context.Products
                    .Include(p => p.ProductVariants)
                    .Where(p => matchedIds.Contains(p.ProductId))
                    .ToListAsync();

                contextBuilder.AppendLine("\nTHÔNG TIN SẢN PHẨM KHÁCH CÓ THỂ ĐANG HỎI:");
                foreach (var p in matchedProducts)
                {
                    contextBuilder.AppendLine($"- Tên: {p.ProductName} (Mã: {p.ProductId})");
                    contextBuilder.AppendLine($"  + Giá: {p.ProductVariants.FirstOrDefault()?.CurrentPrice:N0}đ");
                    contextBuilder.AppendLine("  + Tồn kho:");
                    foreach (var variant in p.ProductVariants)
                    {
                        contextBuilder.AppendLine($"    * Size {variant.Size}, Màu {variant.Color}: {(variant.StockQuantity > 0 ? $"Còn {variant.StockQuantity} cái" : "Hết hàng")}");
                    }
                }
            }
            else
            {
                // Lấy ngẫu nhiên vài sản phẩm nổi bật làm context
                var popProducts = await _context.Products.Include(p => p.ProductVariants).Take(5).Select(p => new { p.ProductName, Price = p.ProductVariants.FirstOrDefault().CurrentPrice }).ToListAsync();
                contextBuilder.AppendLine("\nMỘT SỐ SẢN PHẨM NỔI BẬT CỦA SHOP:");
                foreach (var p in popProducts)
                {
                    contextBuilder.AppendLine($"- {p.ProductName}: {p.Price:N0}đ");
                }
            }
        }

        // 1.3 Lấy thông tin bảng size từ size_chart.txt
        string sizeChart = "";
        try
        {
            if (System.IO.File.Exists("size_chart.txt"))
            {
                sizeChart = await System.IO.File.ReadAllTextAsync("size_chart.txt");
            }
        }
        catch { }

        // 2. Tạo System Prompt ràng buộc
        var systemPrompt = $@"Bạn là trợ lý ảo thân thiện của cửa hàng E-commerce. Nhiệm vụ duy nhất của bạn là hỗ trợ khách hàng về sản phẩm, tư vấn size, và kiểm tra đơn hàng.
TỪ CHỐI mọi câu hỏi không liên quan đến mua sắm, cửa hàng, sản phẩm hoặc đơn hàng. Hãy nói: 'Tôi chỉ là trợ lý bán hàng, tôi không thể giúp bạn điều đó.'
Dưới đây là thông tin lấy từ hệ thống để bạn trả lời khách (KHÔNG ĐƯỢC TỰ BỊA DATA):

{contextBuilder.ToString()}

BẢNG SIZE CỦA SHOP (Dùng để tư vấn nếu khách hỏi):
{sizeChart}

YÊU CẦU:
- Trả lời ngắn gọn, lịch sự, bằng tiếng Việt.
- Dùng thông tin đơn hàng/giỏ hàng/sản phẩm/danh mục/voucher/promotion ở trên để trả lời chính xác.
- Nếu khách hỏi đơn hàng/giỏ hàng mà hệ thống báo trống, hãy trả lời đúng sự thật là giỏ hàng đang trống hoặc chưa có đơn hàng.
- Nếu khách hỏi một mã sản phẩm cụ thể mà trong dữ liệu không có, hãy nói 'Dạ, tôi chưa tìm thấy thông tin sản phẩm này, bạn kiểm tra lại tên/mã nhé'.
- Nếu khách hỏi tìm sản phẩm theo danh mục, hãy tư vấn dựa trên danh mục và các sản phẩm nổi bật có trong dữ liệu.
- Hãy chủ động nhắc đến các Voucher hoặc Promotion nếu nó có vẻ phù hợp để khuyến khích khách hàng mua sắm.";

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

        // Tải ảnh từ Cloudinary nếu có
        string? base64Image = null;
        string? mimeType = null;
        if (!string.IsNullOrEmpty(imageName))
        {
            try
            {
                string imageUrl = $"https://res.cloudinary.com/dss8hptah/image/upload/images/messages/{imageName}";
                var imgBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                base64Image = Convert.ToBase64String(imgBytes);
                mimeType = "image/jpeg";
                if (imageName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) mimeType = "image/png";
                else if (imageName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) mimeType = "image/webp";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi tải ảnh từ Cloudinary cho AI: " + ex.Message);
            }
        }

        var contentsList = new System.Collections.Generic.List<object>();

        if (history != null && history.Any())
        {
            foreach (var h in history)
            {
                contentsList.Add(new
                {
                    role = h.Role,
                    parts = new[] { new { text = h.Text } }
                });
            }
        }

        var userParts = new System.Collections.Generic.List<object>
        {
            new { text = userMessage }
        };

        if (!string.IsNullOrEmpty(base64Image))
        {
            userParts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = base64Image
                }
            });
        }

        contentsList.Add(new
        {
            role = "user",
            parts = userParts.ToArray()
        });

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = contentsList
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Gemini API Error: " + err);
            return "Xin lỗi, hệ thống AI đang gặp sự cố. Chi tiết lỗi: " + err;
        }

        var jsonStr = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonStr);
        var replyText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString();

        return replyText?.Trim() ?? "Không có phản hồi.";
    }
}
