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
        _apiKey = config["Gemini:ApiKey"];
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
            contextBuilder.AppendLine("CUSTOMER'S ORDER INFORMATION:");
            foreach (var order in recentOrders)
            {
                contextBuilder.AppendLine($"- Order #{order.Id} placed on {order.OrderDate:dd/MM/yyyy}: Status '{order.Status}', Total amount: {order.TotalAmount:N0} VND");
            }
        }
        else
        {
            contextBuilder.AppendLine("CUSTOMER'S ORDER INFORMATION: The customer currently has no orders in the system.");
        }

        // 1.1b Lấy thông tin giỏ hàng
        var cartItems = await _context.CartItems
            .Include(c => c.ProductVariant)
            .ThenInclude(v => v.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cartItems.Any())
        {
            contextBuilder.AppendLine("CUSTOMER'S CART INFORMATION:");
            foreach (var item in cartItems)
            {
                var p = item.ProductVariant.Product;
                contextBuilder.AppendLine($"- Product: {p.ProductName} (Size {item.ProductVariant.Size}, Color {item.ProductVariant.Color}), Quantity: {item.Quantity}, Price: {item.ProductVariant.CurrentPrice:N0} VND");
            }
        }
        else
        {
            contextBuilder.AppendLine("CUSTOMER'S CART INFORMATION: The cart is currently empty.");
        }

        // 1.1c Lấy danh sách danh mục
        var categories = await _context.Categories.Where(c => c.IsActived).Select(c => new { c.Id, c.Name }).ToListAsync();
        if (categories.Any())
        {
            contextBuilder.AppendLine("STORE'S PRODUCT CATEGORIES:");
            foreach (var c in categories)
            {
                contextBuilder.AppendLine($"- {c.Name} (ID: {c.Id})");
            }
        }

        // 1.1d Lấy danh sách Voucher của User đang hoạt động
        var activeVouchers = await _context.UserVouchers
            .Include(uv => uv.Voucher)
            .Where(uv => uv.UserId == userId && !uv.IsUsed && uv.Voucher != null && uv.Voucher.IsActived && uv.Voucher.EndDate >= DateTime.Now)
            .Select(uv => uv.Voucher)
            .ToListAsync();
        if (activeVouchers.Any())
        {
            contextBuilder.AppendLine("\nCUSTOMER'S AVAILABLE VOUCHERS:");
            foreach (var v in activeVouchers)
            {
                contextBuilder.AppendLine($"- Code '{v.Code}': Discount {v.DiscountValue:N0} VND, Min order: {v.MinOrderValue:N0} VND, Expires on: {v.EndDate:dd/MM/yyyy}");
            }
        }

        // 1.1e Lấy danh sách Promotion
        var activePromotions = await _context.Promotions.Where(p => p.IsActived && p.EndDate >= DateTime.Now).ToListAsync();
        if (activePromotions.Any())
        {
            contextBuilder.AppendLine("\nACTIVE PROMOTIONS (MAJOR DISCOUNTS):");
            foreach (var p in activePromotions)
            {
                contextBuilder.AppendLine($"- {p.Title}: {p.Description} (Discount {p.DiscountPercentage:N0}%, Expires on: {p.EndDate:dd/MM/yyyy})");
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
                contextBuilder.AppendLine($"\nINFORMATION OF THE PRODUCT THE CUSTOMER IS VIEWING (ID: {product.ProductId}, Name: {product.ProductName}):");
                contextBuilder.AppendLine($"- Selling price: {product.ProductVariants.FirstOrDefault()?.CurrentPrice:N0} VND");
                contextBuilder.AppendLine("- Inventory by variant:");
                foreach (var variant in product.ProductVariants)
                {
                    contextBuilder.AppendLine($"  + Size {variant.Size}, Color {variant.Color}: {(variant.StockQuantity > 0 ? $"{variant.StockQuantity} in stock" : "Out of stock")}");
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

                contextBuilder.AppendLine("\nPRODUCT INFORMATION THE CUSTOMER MIGHT BE ASKING ABOUT:");
                foreach (var p in matchedProducts)
                {
                    contextBuilder.AppendLine($"- Name: {p.ProductName} (ID: {p.ProductId})");
                    contextBuilder.AppendLine($"  + Price: {p.ProductVariants.FirstOrDefault()?.CurrentPrice:N0} VND");
                    contextBuilder.AppendLine("  + Inventory:");
                    foreach (var variant in p.ProductVariants)
                    {
                        contextBuilder.AppendLine($"    * Size {variant.Size}, Color {variant.Color}: {(variant.StockQuantity > 0 ? $"{variant.StockQuantity} in stock" : "Out of stock")}");
                    }
                }
            }
            else
            {
                // Lấy ngẫu nhiên vài sản phẩm nổi bật làm context
                var popProducts = await _context.Products.Include(p => p.ProductVariants).Take(5).Select(p => new { p.ProductName, Price = p.ProductVariants.FirstOrDefault().CurrentPrice }).ToListAsync();
                contextBuilder.AppendLine("\nSOME FEATURED PRODUCTS FROM THE STORE:");
                foreach (var p in popProducts)
                {
                    contextBuilder.AppendLine($"- {p.ProductName}: {p.Price:N0} VND");
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
        var systemPrompt = $@"You are a friendly virtual assistant for an E-commerce store. Your sole task is to assist customers with products, size recommendations, and checking orders.
DECLINE any questions unrelated to shopping, the store, products, or orders. Simply say: 'I am just a sales assistant, I cannot help you with that.'
Below is the information retrieved from the system for you to answer the customer (DO NOT MAKE UP DATA):

{contextBuilder.ToString()}

STORE'S SIZE CHART (Use this for sizing recommendations if asked):
{sizeChart}

REQUIREMENTS:
- Answer concisely, politely, and in English.
- Use the order/cart/product/category/voucher information above to provide accurate answers.
- If the customer asks about an order/cart and the system says it is empty, state the truth that the cart is empty or there are no orders.
- If the customer asks about a specific product code that is not in the data, say 'I could not find this product information, please double-check the name/code'.
- If the customer asks to find a product by category, recommend based on the category and featured products in the data.
- Proactively mention Vouchers or Promotions if they seem relevant to encourage the customer to shop.";

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

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
        using var client = new HttpClient();
        var response = await client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Gemini API Error: " + err);
            return "Xin lỗi, hệ thống AI hiện đang quá tải hoặc gặp sự cố. Vui lòng thử lại sau ít phút.";
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
