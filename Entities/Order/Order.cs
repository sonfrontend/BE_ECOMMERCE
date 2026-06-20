using System;
using System.Collections.Generic;
using BE_ECOMMERCE.Entities.Auth;
using System.ComponentModel.DataAnnotations;
using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Constants;

namespace BE_ECOMMERCE.Entities.Order;

public class Order : BaseEntity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;

    // Status can be: Pending, Approved, Shipping, Delivered, Cancelled
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime? DeliveredDate { get; set; }
    public string? TransactionId { get; set; }
    public string? VnPayTxnRef { get; set; }
    public string? VnPayPayDate { get; set; }
    public string? Email { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }

    public string PaymentMethod { get; set; } = PaymentMethodConstant.COD;
    public bool IsPaid { get; set; } = false;

    public string? VoucherCode { get; set; }
    public decimal DiscountAmount { get; set; } = 0;

    // Thông tin giao hàng
    public string RecipientName { get; set; }
    public string PhoneNumber { get; set; }
    public string ShippingAddress { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; }
}
