namespace BE_ECOMMERCE.Constants
{
  // static class: Không cần khởi tạo (new) vẫn gọi được
  public static class OrderStatusConstant
  {
    // 1. Nhóm trạng thái Khởi tạo & Thanh toán
    public const string PendingPayment = "PendingPayment"; // Chờ thanh toán (Dành cho đơn QR đang đếm ngược)
    public const string Pending = "Pending";               // Chờ xác nhận (Đơn COD mới đặt, hoặc đơn QR đã quét trả tiền)

    // 2. Nhóm trạng thái Xử lý vận đơn
    public const string Processing = "Processing";         // Đang xử lý (Shop đang nhặt hàng, đóng gói)
    public const string Shipped = "Shipped";               // Đang giao hàng (Đã giao cho Shipper)
    public const string Delivered = "Delivered";           // Đã giao đến nơi (Chờ khách xác nhận)

    // 3. Nhóm trạng thái Kết thúc chu trình
    public const string Completed = "Completed";           // Hoàn thành (Khách đã nhận hàng thành công)

    // 4. Nhóm trạng thái Hủy & Hoàn trả
    public const string Cancelled = "Cancelled";           // Đã hủy (Khách tự hủy, hoặc quá 2 phút không quét QR)
    public const string Refunded = "Refunded";             // Hoàn tiền (Khách trả hàng)
    public const string Disputed = "Disputed";             // Đang khiếu nại (Khách chưa nhận được hàng)
    public const string PendingResolution = "PendingResolution"; // Đang chờ khách xác nhận quyết định giải quyết của Admin
    public const string Resolved = "Resolved";             // Đã giải quyết xong khiếu nại
    public const string Lost = "Lost";                     // Thất lạc do vận chuyển
  }
}
