using MassTransit.Futures.Contracts;

namespace Domain.Features.PriceGrabber.Models;

public sealed record PriceGrabberRequest
{
    public required Guid TrackingId { get; set; }

    public required Guid ProductId { get; set; }

    public required Uri Url { get; set; }
}
