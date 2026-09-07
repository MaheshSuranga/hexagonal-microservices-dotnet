import React, { useState } from 'react';
import { orderApi, ApiClientError } from './api/orderApi';
import type { OrderResponse, OrderLineDto } from './api/types/order.types';

export default function App() {
  // Order Creation State
  const [customerName, setCustomerName] = useState('Alice Engineer');
  const [productName, setProductName] = useState('NVMe M.2 2TB SSD');
  const [quantity, setQuantity] = useState(1);
  const [unitPrice, setUnitPrice] = useState(140);
  const [submitting, setSubmitting] = useState(false);

  // Active/Created Order State
  const [createdOrder, setCreatedOrder] = useState<OrderResponse | null>(null);
  const [searchId, setSearchId] = useState('');
  const [lookupResult, setLookupResult] = useState<{
    order: OrderResponse;
    cacheStatus?: string;
    durationMs?: string;
  } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loadingLookup, setLoadingLookup] = useState(false);

  async function handleCreateOrder(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const items: OrderLineDto[] = [{ productName, quantity, unitPrice }];
      const response = await orderApi.createOrder({ customerName, items });
      setCreatedOrder(response);
      setSearchId(response.id);
      // Auto-lookup to demonstrate the immediate miss/hit
      await handleLookup(response.id);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.errorResponse?.message || err.message);
      } else {
        setError((err as Error).message);
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleLookup(idToQuery?: string) {
    const id = idToQuery || searchId;
    if (!id.trim()) return;

    setError(null);
    setLoadingLookup(true);

    try {
      const result = await orderApi.getOrderById(id.trim());
      setLookupResult(result);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.errorResponse?.message || err.message);
      } else {
        setError((err as Error).message);
      }
      setLookupResult(null);
    } finally {
      setLoadingLookup(false);
    }
  }

  return (
    <div className="app-container">
      <header className="app-header">
        <div>
          <h1 className="brand-title">Order Management Portal</h1>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', marginTop: '0.25rem' }}>
            Hexagonal Microservices Architecture with Redis Cache-Aside &amp; Transactional Outbox
          </p>
        </div>
        <div className="brand-badge">
          <span>●</span> .NET 8 + Vite React TS
        </div>
      </header>

      {error && (
        <div className="alert-box alert-error">
          <strong>Error: </strong> {error}
        </div>
      )}

      <div className="grid-layout">
        {/* Left Column: Create Order Form */}
        <div className="card">
          <h2 className="card-title">Place New Order</h2>
          <form onSubmit={handleCreateOrder}>
            <div className="form-group">
              <label htmlFor="customerName">Customer Name</label>
              <input
                id="customerName"
                type="text"
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                required
              />
            </div>

            <div className="items-section">
              <label>Order Items</label>
              <div className="item-row">
                <input
                  type="text"
                  placeholder="Product Name"
                  value={productName}
                  onChange={(e) => setProductName(e.target.value)}
                  required
                />
                <input
                  type="number"
                  min="1"
                  placeholder="Qty"
                  value={quantity}
                  onChange={(e) => setQuantity(parseInt(e.target.value) || 1)}
                  required
                />
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  placeholder="Price"
                  value={unitPrice}
                  onChange={(e) => setUnitPrice(parseFloat(e.target.value) || 0)}
                  required
                />
              </div>
            </div>

            <button type="submit" className="btn btn-primary" style={{ width: '100%' }} disabled={submitting}>
              {submitting ? 'Submitting...' : 'Submit Order'}
            </button>
          </form>

          {createdOrder && (
            <div className="order-result-box">
              <div style={{ color: 'var(--accent-success)', fontWeight: 600, marginBottom: '0.5rem' }}>
                ✓ Order Created &amp; Outbox Staged!
              </div>
              <div className="metric-row">
                <span>Order ID:</span>
                <code style={{ color: 'var(--accent-cyan)' }}>{createdOrder.id}</code>
              </div>
              <div className="metric-row">
                <span>Total Amount:</span>
                <strong>${createdOrder.totalAmount.toFixed(2)}</strong>
              </div>
            </div>
          )}
        </div>

        {/* Right Column: Query & Cache Benchmark */}
        <div className="card">
          <h2 className="card-title">Query Order &amp; Inspect Redis Latency</h2>
          <div className="form-group">
            <label htmlFor="searchId">Search by Order ID</label>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
              <input
                id="searchId"
                type="text"
                placeholder="Paste Order UUID..."
                value={searchId}
                onChange={(e) => setSearchId(e.target.value)}
              />
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => handleLookup()}
                disabled={loadingLookup}
              >
                {loadingLookup ? 'Querying...' : 'Fetch'}
              </button>
            </div>
          </div>

          {lookupResult && (
            <div className="order-result-box">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
                <span style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>Cache Diagnostics</span>
                <span className={`badge ${lookupResult.cacheStatus === 'HIT' ? 'badge-hit' : 'badge-miss'}`}>
                  {lookupResult.cacheStatus || 'DIRECT'}
                </span>
              </div>

              <div className="metric-row">
                <span>Database/Cache Latency:</span>
                <strong style={{ color: lookupResult.cacheStatus === 'HIT' ? 'var(--accent-success)' : 'var(--accent-warning)' }}>
                  {lookupResult.durationMs ? `${lookupResult.durationMs} ms` : 'N/A'}
                </strong>
              </div>
              <div className="metric-row">
                <span>Customer:</span>
                <span>{lookupResult.order.customerName}</span>
              </div>
              <div className="metric-row">
                <span>Created At:</span>
                <span>{new Date(lookupResult.order.createdAtUtc).toLocaleTimeString()}</span>
              </div>
              <div className="metric-row">
                <span>Total Amount:</span>
                <strong>${lookupResult.order.totalAmount.toFixed(2)}</strong>
              </div>

              <div style={{ marginTop: '1rem' }}>
                <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Items:</span>
                <ul style={{ listStyle: 'none', marginTop: '0.5rem' }}>
                  {lookupResult.order.items.map((item, idx) => (
                    <li key={idx} className="metric-row" style={{ padding: '0.25rem 0' }}>
                      <span>{item.quantity}x {item.productName}</span>
                      <span>${item.subtotal.toFixed(2)}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
