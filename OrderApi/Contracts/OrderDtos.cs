namespace OrderApi.Contracts;

public record CreateOrderRequest(
    string CustomerName,
    List<CreateOrderLineRequest> Items
);

public record CreateOrderLineRequest(
    string ProductName,
    int Quantity,
    decimal UnitPrice
);

public record OrderResponse(
    Guid Id,
    string CustomerName,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    List<OrderLineResponse> Items
);

public record OrderLineResponse(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);
