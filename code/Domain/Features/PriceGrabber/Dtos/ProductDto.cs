namespace Domain.Features.PriceGrabber.Dtos;

public sealed record ProductDto
{
    public required Uri Url { get; set; }

    public bool KeepQueryParams { get; set; } = false;
}