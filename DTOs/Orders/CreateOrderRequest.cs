using System.Collections.Generic;

namespace BE_ECOMMERCE.DTOs.Orders
{
    public class CreateOrderRequest
    {
        public string RecipientName { get; set; }
        public string PhoneNumber { get; set; }
        public string ShippingAddress { get; set; }
        public decimal ShippingFee { get; set; }
        public string PaymentMethod { get; set; }
        public string? VoucherCode { get; set; }
        public string? Email { get; set; }
        public List<int> SelectedCartItemIds { get; set; }
    }
}
