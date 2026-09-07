import { useState } from 'react';
import { orderClient, ApiError } from './api/httpClient';
import { OrderForm } from './components/OrderForm';
import type { OrderResponse } from './api/types/order.types';

export default function App() {
  const [searchId, setSearchId] = useState('');
  const [lookupResult, setLookupResult] = useState<{
    order: OrderResponse;
    cacheStatus?: string;
    durationMs?: string;
  } | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [loadingLookup, setLoadingLookup] = useState(false);

  async function handleLookup(idToQuery?: string) {
    const id = idToQuery || searchId;
    if (!id.trim()) return;

    setLookupError(null);
    setLoadingLookup(true);

    try {
      const result = await orderClient.getOrderById(id.trim());
      setLookupResult(result);
    } catch (err) {
      if (err instanceof ApiError) {
        setLookupError(err.message);
      } else {
        setLookupError((err as Error).message);
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
            Hexagonal Microservices with Transactional Outbox &amp; Redis Cache-Aside
          </p>
        </div>
        <div className="brand-badge">
          <span>●</span> .NET 8 + Vite React TS
        </div>
      </header>

      <div className="grid-layout">
        {/* Left Column: Decoupled Order Form UI Component */}
        <OrderForm />

        {/* Right Column: Order Query & Cache Latency Telemetry */}
        <div className="card">
          <h2 className="card-title">Query Order &amp; Inspect Cache Telemetry</h2>
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
                disabled={loadingLookup || !searchId.trim()}
              >
                {loadingLookup ? 'Querying...' : 'Fetch'}
              </button>
            </div>
          </div>

          {lookupError && (
            <div className="alert-box alert-error" style={{ marginTop: '1rem' }}>
              {lookupError}
            </div>
          )}

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
