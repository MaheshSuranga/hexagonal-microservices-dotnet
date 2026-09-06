namespace OrderApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using OrderApi.Data;
using OrderApi.Services;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        // Business Rule Leakage: Controller performing core business validation
        if (order.Items == null || order.Items.Count == 0)
        {
            return BadRequest(new { error = "Validation Failed", message = "Order must contain at least one item." });
        }

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            return BadRequest(new { error = "Validation Failed", message = "Customer name is required." });
        }

        // Entity Exposure Flaw: The database model is accepted directly from the client request body.
        // If a malicious client passes TotalAmount, CreatedAt, or Id, model binding binds directly to the entity.
        var createdOrder = await _orderService.CreateOrderAsync(order);

        // Entity Exposure Flaw: The database model is serialized directly to the HTTP response.
        return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, createdOrder);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound(new { message = $"Order {id} not found." });
        }

        // Entity Exposure Flaw: EF Core entity structure is leaked directly out of the API.
        return Ok(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(orders);
    }
}
