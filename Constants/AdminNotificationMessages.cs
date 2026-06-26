namespace BE_ECOMMERCE.Constants;

public static class AdminNotificationMessages
{
    public static string GetMessage(string action, string details)
    {
        return action.ToUpper() switch
        {
            "ADD_TO_CART" => $"Người dùng vừa thêm sản phẩm vào giỏ hàng. Chi tiết: {details}",
            "CHECKOUT" => $"Người dùng vừa đặt một đơn hàng mới. Chi tiết: {details}",
            "COMPLETE_ORDER" => $"Người dùng vừa xác nhận đã nhận được hàng. Chi tiết: {details}",
            "REGISTER" => $"Một người dùng mới vừa đăng ký tài khoản. Email/Tên: {details}",
            "REQUEST_REFUND" => $"Người dùng vừa yêu cầu đổi trả cho đơn hàng. Chi tiết: {details}",
            "FAVORITE" => $"Người dùng vừa thêm sản phẩm vào danh sách yêu thích. Chi tiết: {details}",
            "ACCEPT_DISPUTE" => $"Người dùng đã CHẤP NHẬN phương án giải quyết khiếu nại. Chi tiết: {details}",
            "REJECT_DISPUTE" => $"Người dùng TỪ CHỐI phương án giải quyết khiếu nại. Chi tiết: {details}",
            _ => $"Người dùng vừa thực hiện hành động: {action}. Chi tiết: {details}"
        };
    }

    public static string GetTitle(string action)
    {
        return action.ToUpper() switch
        {
            "ADD_TO_CART" => "Lượt thêm giỏ hàng mới",
            "CHECKOUT" => "Đơn đặt hàng mới",
            "COMPLETE_ORDER" => "Đơn hàng hoàn thành",
            "REGISTER" => "Người dùng mới",
            "REQUEST_REFUND" => "Yêu cầu đổi trả",
            "FAVORITE" => "Lượt thích sản phẩm",
            "ACCEPT_DISPUTE" => "Đồng ý giải quyết khiếu nại",
            "REJECT_DISPUTE" => "Từ chối giải quyết khiếu nại",
            _ => "Thông báo hệ thống"
        };
    }
}
