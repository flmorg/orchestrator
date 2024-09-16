using System.ComponentModel.DataAnnotations;

namespace Data.Features.PriceGrabber.Models;

public sealed class Store
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    
    [MaxLength(100)]
    public required string Domain { get; init; }
    
    [Timestamp]
    public byte[] Version { get; set; }
}