using Data.DbContexts;
using Data.Features.PriceGrabber.Enums;
using Data.Features.PriceGrabber.Models;
using Domain.Features.PriceGrabber.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.PriceGrabber.Services;

public sealed class PriceGrabberService
{
    private readonly OrchestratorContext _orchestratorContext;

    public PriceGrabberService(OrchestratorContext orchestratorContext)
    {
        _orchestratorContext = orchestratorContext ?? throw new ArgumentNullException(nameof(orchestratorContext));
    }

    public async Task AddStore(StoreDto storeDto)
    {
        Store store = new()
        {
            Domain = storeDto.Url.Host
        };
        
        await _orchestratorContext.AddAsync(store);
        await _orchestratorContext.SaveChangesAsync();
    }

    public async Task AddProduct(ProductDto productDto)
    {
        Store? store = await _orchestratorContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Domain.Equals(productDto.Url.Host));

        if (store is null)
        {
            throw new Exception($"store not found | {productDto.Url.Host}");
        }
        
        UriBuilder uriBuilder = new(productDto.Url)
        {
            Query = productDto.KeepQueryParams
                ? productDto.Url.Query
                : string.Empty
        };

        Product product = new()
        {
            StoreId = store.Id,
            Url = uriBuilder.Uri,
            State = ProductState.Scheduled
        };
        
        await _orchestratorContext.AddAsync(product);
        await _orchestratorContext.SaveChangesAsync();
    }
}
