namespace Domain.Features.PriceGrabber.Dtos;

public sealed record StoreDto
{
    public required Uri Url { get; set; }
}