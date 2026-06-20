using System;
using System.ComponentModel.DataAnnotations;

namespace BE_ECOMMERCE.Entities.Promotion;

public class Promotion : BaseEntity
{
    public int Id { get; set; }

    [MaxLength(255)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public required string ImageUrl { get; set; }

    public decimal DiscountPercentage { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [MaxLength(500)]
    public string? Link { get; set; }

}

