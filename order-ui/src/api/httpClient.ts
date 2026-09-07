import type { CreateOrderRequest, OrderResponse } from './types/order.types';

export type ApiErrorKind = 'VALIDATION' | 'DEGRADED' | 'NOT_FOUND' | 'NETWORK' | 'UNKNOWN';

export interface NormalizedError {
  readonly status: number;
  readonly kind: ApiErrorKind;
  readonly message: string;
  readonly details?: string | string[];
}

export class ApiError extends Error implements NormalizedError {
  constructor(
    public readonly status: number,
    public readonly kind: ApiErrorKind,
    message: string,
    public readonly details?: string | string[]
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const BASE_URL = import.meta.env.VITE_API_URL || '/api';

/**
 * Normalizes HTTP response errors and network exceptions into predictable ApiError instances.
 */
async function normalizeError(response: Response): Promise<ApiError> {
  const status = response.status;
  let message = `Request failed with status ${status}`;
  let details: string | string[] | undefined;

  try {
    const data = await response.json();
    if (data && typeof data === 'object') {
      message = data.message || data.error || data.title || message;
      if (data.errors) {
        details = Object.values(data.errors).flat() as string[];
      }
    }
  } catch {
    message = response.statusText || message;
  }

  let kind: ApiErrorKind = 'UNKNOWN';
  if (status === 400) {
    kind = 'VALIDATION';
  } else if (status === 404) {
    kind = 'NOT_FOUND';
  } else if (status === 502 || status === 503 || status === 504) {
    kind = 'DEGRADED';
    message = 'The service is temporarily degraded or undergoing maintenance. Please retry shortly.';
  } else if (status >= 500) {
    kind = 'DEGRADED';
  }

  return new ApiError(status, kind, message, details);
}

/**
 * Strongly-typed HTTP Client using native fetch.
 */
export const orderClient = {
  /**
   * Places an order and atomically commits to the backend outbox.
   */
  async createOrder(request: CreateOrderRequest): Promise<OrderResponse> {
    try {
      const response = await fetch(`${BASE_URL}/orders`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw await normalizeError(response);
      }

      return (await response.json()) as OrderResponse;
    } catch (err) {
      if (err instanceof ApiError) {
        throw err;
      }
      // Catch network-level drops (e.g. backend down or CORS refusal)
      throw new ApiError(0, 'NETWORK', 'Unable to reach backend server. Please verify API is running.');
    }
  },

  /**
   * Retrieves an order by ID with Redis cache telemetry.
   */
  async getOrderById(id: string): Promise<{ order: OrderResponse; cacheStatus?: string; durationMs?: string }> {
    try {
      const response = await fetch(`${BASE_URL}/orders/${id}`, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
        },
      });

      if (!response.ok) {
        throw await normalizeError(response);
      }

      const order = (await response.json()) as OrderResponse;
      return {
        order,
        cacheStatus: response.headers.get('X-Cache') ?? undefined,
        durationMs: response.headers.get('X-Query-Duration-Ms') ?? undefined,
      };
    } catch (err) {
      if (err instanceof ApiError) {
        throw err;
      }
      throw new ApiError(0, 'NETWORK', 'Unable to reach backend server. Please verify API is running.');
    }
  },
};
