using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Authorization;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _productService.GetAllProductsAsync();
        return Ok(products);
    }

    [Authorize(Policy = Permissions.ProductsRead)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();
        
        return Ok(product);
    }

    [Authorize(Policy = Permissions.ProductsCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(Product product)
    {
        var createdProduct = await _productService.CreateProductAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, createdProduct);
    }

    [Authorize(Policy = Permissions.ProductsUpdate)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Product product)
    {
        var updated = await _productService.UpdateProductAsync(id, product);
        if (!updated) return NotFound();

        return NoContent();
    }

    [Authorize(Policy = Permissions.ProductsDelete)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _productService.DeleteProductAsync(id);
        if (!deleted) return NotFound();

        return NoContent();
    }
}

