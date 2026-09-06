namespace OrderApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using OrderApi.Contracts;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using OrderApi.Domain.Ports;

/// <summary>
/// Driving (Inbound) Adapter.
/// Translates incoming HTTP requests to Domain models and dispatches them to Ports.
/// It contains NO domain business rules and NEVER exposes database entities.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;

    public OrdersController(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            // 1. Translate DTOs to Domain Objects
            var lines = (request.Items ?? new List<CreateOrderLineRequest>())
                .Select(i => new OrderLine(i.ProductName, i.Quantity, i.UnitPrice));

            // 2. Instantiate Aggregate via Factory Method (Invariants enforced by the Domain!)
            var order = Order.Create(request.CustomerName, lines);

            // 3. Persist via Driven Port
            await _orderRepository.AddAsync(order, ct);

            // 4. Map Domain Model to Response DTO
            var response = MapToResponse(order);

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
        }
        catch (DomainException ex)
        {
            // Catch domain invariant violations and translate to HTTP 400 Bad Request
            return BadRequest(new { error = "Domain Invariant Violation", message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct);
        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} was not found." });
        }

        return Ok(MapToResponse(order));
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerName,
            order.CreatedAtUtc,
            order.TotalAmount,
            order.Lines.Select(l => new OrderLineResponse(
                l.ProductName,
                l.Quantity,
                l.UnitPrice,
                l.Subtotal
            )).ToList()
        );
    }
}
