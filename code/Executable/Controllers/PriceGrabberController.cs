using Domain.Features.PriceGrabber.Dtos;
using Domain.Features.PriceGrabber.Services;
using Microsoft.AspNetCore.Mvc;

namespace Executable.Controllers;

[ApiController]
[Route("[controller]")]
public class PriceGrabberController : ControllerBase
{
    private readonly PriceGrabberService _service;
    
    public PriceGrabberController(PriceGrabberService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> AddStore(StoreDto storeDto)
    {
        await _service.AddStore(storeDto);
        return Ok();
    }
    
    [HttpPost("[action]")]
    public async Task<IActionResult> AddProduct(ProductDto productDto)
    {
        await _service.AddProduct(productDto);
        return Ok();
    }
}
