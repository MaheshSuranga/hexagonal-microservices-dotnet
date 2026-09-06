namespace OrderApi.Controllers;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderApi.Contracts;
using OrderApi.Contracts.Events;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using OrderApi.Domain.Ports;

/// <summary>
/// Driving (Inbound) Adapter.
/// Translates incoming HTTP requests to Domain models, coordinates persistence,
/// and writes integration events to the transactional outbox.
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

            // 2. Instantiate Aggregate via Factory Method (Invariants enforced by Domain Core)
            var order = Order.Create(request.CustomerName, lines);

            // 3. Create Integration Event and stage in OutboxMessage
            var orderPlacedEvent = OrderPlacedEvent.Create(order.Id, order.CustomerName, order.TotalAmount);
            var outboxMessage = OutboxMessage.Create(
                type: nameof(OrderPlacedEvent),
                payload: JsonSerializer.Serialize(orderPlacedEvent)
            );

            // -----------------------------------------------------------------
            // FAILURE SIMULATION: Verify Atomic Rollback
            // -----------------------------------------------------------------
            if (Request.Headers.ContainsKey("X-Simulate-Commit-Failure") ||
                request.CustomerName.Contains("SIMULATE_COMMIT_FAILURE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "[TRANSACTION-ROLLBACK] Simulated failure occurred prior to commit. Verifying zero state mutation.");
            }

            // 4. ATOMIC COMMIT: Persist both Order and OutboxMessage in a single database transaction
            await _orderRepository.AddWithOutboxAsync(order, outboxMessage, ct);

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
