import type { CreateOrderRequest, OrderResponse, ApiErrorResponse } from './types/order.types';

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

export class ApiClientError extends Error {
  constructor(
    public readonly status: number,
    public readonly errorResponse?: ApiErrorResponse
  ) {
    super(errorResponse?.message || `HTTP ${status}`);
    this.name = 'ApiClientError';
  }
}

export const orderApi = {
  /**
   * Submits a new order command to the backend.
   */
  async createOrder(request: CreateOrderRequest): Promise<OrderResponse> {
    const response = await fetch(`${API_BASE_URL}/orders`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const errorJson = (await response.json().catch(() => undefined)) as ApiErrorResponse | undefined;
      throw new ApiClientError(response.status, errorJson);
    }

    return (await response.json()) as OrderResponse;
  },

  /**
   * Retrieves an existing order by ID (with caching & latency headers).
   */
  async getOrderById(id: string): Promise<{ order: OrderResponse; cacheStatus?: string; durationMs?: string }> {
    const response = await fetch(`${API_BASE_URL}/orders/${id}`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
    });

    if (!response.ok) {
      const errorJson = (await response.json().catch(() => undefined)) as ApiErrorResponse | undefined;
      throw new ApiClientError(response.status, errorJson);
    }

    const order = (await response.json()) as OrderResponse;
    return {
      order,
      cacheStatus: response.headers.get('X-Cache') ?? undefined,
      durationMs: response.headers.get('X-Query-Duration-Ms') ?? undefined,
    };
  },
};
