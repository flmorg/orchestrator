using System.ComponentModel.DataAnnotations;
using Data.Features.PriceGrabber.Enums;

namespace Data.Features.PriceGrabber.Models;

public sealed class Product
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required Uri Url { get; set; }
    
    public required Guid StoreId { get; set; }
    
    public Store Store { get; set; }

    public required ProductState State { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastRefreshedAt { get; set; }

    [Timestamp]
    public byte[] Version { get; set; }
}
