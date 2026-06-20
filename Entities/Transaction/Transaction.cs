using System;
using BE_ECOMMERCE.Entities.Product;

namespace BE_ECOMMERCE.Entities.Transaction;

public class Transaction : BaseEntity
{
    public int Id { get; set; }
    public DateTime TDat { get; set; }
    public string CustomerId { get; set; }
    
    public int VariantId { get; set; }
    public virtual ProductVariant ProductVariant { get; set; }
    
    public double Price { get; set; }
    public int SalesChannelId { get; set; }
}