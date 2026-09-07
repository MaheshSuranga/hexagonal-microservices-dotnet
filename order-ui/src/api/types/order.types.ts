/**
 * Type contracts mirroring OrderApi ASP.NET Core DTOs.
 * Strict typing enforced: No usage of `any`.
 */

/**
 * DTO for creating an individual line item inside an order.
 * Mirrors OrderApi.Contracts.CreateOrderLineRequest
 */
export interface OrderLineDto {
  readonly productName: string;
  readonly quantity: number;
  readonly unitPrice: number;
}

/**
 * Command payload for creating a new order.
 * Mirrors OrderApi.Contracts.CreateOrderRequest
 */
export interface CreateOrderRequest {
  readonly customerName: string;
  readonly items: readonly OrderLineDto[];
}

/**
 * Line item representation returned from the server.
 * Mirrors OrderApi.Contracts.OrderLineResponse
 */
export interface OrderLineResponse {
  readonly productName: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly subtotal: number;
}

/**
 * Full Order representation returned from the server.
 * Mirrors OrderApi.Contracts.OrderResponse
 */
export interface OrderResponse {
  readonly id: string; // UUID v4 format
  readonly customerName: string;
  readonly createdAtUtc: string; // ISO 8601 UTC timestamp
  readonly totalAmount: number;
  readonly items: readonly OrderLineResponse[];
}

/**
 * Standard API error contract for bad requests or domain rule violations.
 */
export interface ApiErrorResponse {
  readonly error?: string;
  readonly message?: string;
  readonly status?: number;
}
