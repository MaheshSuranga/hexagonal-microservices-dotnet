namespace OrderApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using OrderApi.Contracts;
using OrderApi.Contracts.Events;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using OrderApi.Domain.Ports;

/// <summary>
/// Driving (Inbound) Adapter.
/// Translates incoming HTTP requests to Domain models, coordinates persistence,
/// and dispatches integration events to the event publisher port.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventPublisher _eventPublisher;

    public OrdersController(IOrderRepository orderRepository, IEventPublisher eventPublisher)
    {
        _orderRepository = orderRepository;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            // 1. Translate DTOs to Domain Objects
            var lines = (request.Items ?? new List<CreateOrderLineRequest>())
                .Select(i => new OrderLine(i.ProductName, i.Quantity, i.UnitPrice));

            // 2. Instantiate Aggregate via Factory Method (Invariants enforced by Domain Core)
            var order = Order.Create(request.CustomerName, lines);

            // 3. Persist via Repository Driven Port
            await _orderRepository.AddAsync(order, ct);

            // 4. Publish Integration Event via Messaging Driven Port
            var orderPlacedEvent = OrderPlacedEvent.Create(order.Id, order.CustomerName, order.TotalAmount);
            await _eventPublisher.PublishAsync(orderPlacedEvent, ct);

            // 5. Map Domain Model to Response DTO
            var response = MapToResponse(order);

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
        }
        catch (DomainException ex)
        {
            // Catch domain invariant violations and return HTTP 400 Bad Request
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
