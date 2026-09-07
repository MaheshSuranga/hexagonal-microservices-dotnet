import { useState, useCallback } from 'react';
import { orderClient, ApiError } from '../api/httpClient';
import type { CreateOrderRequest, OrderResponse } from '../api/types/order.types';

export type SubmissionStatus = 'idle' | 'loading' | 'success' | 'error';

export interface UseCreateOrderResult {
  readonly status: SubmissionStatus;
  readonly isLoading: boolean;
  readonly createdOrder: OrderResponse | null;
  readonly error: ApiError | null;
  readonly submitOrder: (request: CreateOrderRequest) => Promise<OrderResponse | null>;
  readonly reset: () => void;
}

/**
 * Custom Hook isolating network coordination and mutation state from UI presentation.
 */
export function useCreateOrder(): UseCreateOrderResult {
  const [status, setStatus] = useState<SubmissionStatus>('idle');
  const [createdOrder, setCreatedOrder] = useState<OrderResponse | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  const submitOrder = useCallback(async (request: CreateOrderRequest): Promise<OrderResponse | null> => {
    setStatus('loading');
    setError(null);

    try {
      const result = await orderClient.createOrder(request);
      setCreatedOrder(result);
      setStatus('success');
      return result;
    } catch (err) {
      const apiErr = err instanceof ApiError ? err : new ApiError(0, 'UNKNOWN', (err as Error).message);
      setError(apiErr);
      setStatus('error');
      return null;
    }
  }, []);

  const reset = useCallback(() => {
    setStatus('idle');
    setCreatedOrder(null);
    setError(null);
  }, []);

  return {
    status,
    isLoading: status === 'loading',
    createdOrder,
    error,
    submitOrder,
    reset,
  };
}
