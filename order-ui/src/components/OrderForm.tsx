import React, { useState } from 'react';
import { useCreateOrder } from '../hooks/useCreateOrder';
import type { OrderLineDto } from '../api/types/order.types';

export function OrderForm() {
  const { isLoading, createdOrder, error, submitOrder, reset } = useCreateOrder();

  const [customerName, setCustomerName] = useState('');
  const [items, setItems] = useState<OrderLineDto[]>([
    { productName: 'Mechanical Keyboard', quantity: 1, unitPrice: 85.00 },
  ]);

  function handleAddItem() {
    setItems((prev) => [...prev, { productName: '', quantity: 1, unitPrice: 10.00 }]);
  }

  function handleRemoveItem(index: number) {
    setItems((prev) => prev.filter((_, i) => i !== index));
  }

  function handleUpdateItem(index: number, field: keyof OrderLineDto, value: string | number) {
    setItems((prev) =>
      prev.map((item, i) => (i === index ? { ...item, [field]: value } : item))
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    await submitOrder({
      customerName: customerName.trim(),
      items: items.map((i) => ({
        productName: i.productName.trim(),
        quantity: Number(i.quantity),
        unitPrice: Number(i.unitPrice),
      })),
    });
  }

  function handleStartNew() {
    reset();
    setCustomerName('');
    setItems([{ productName: '', quantity: 1, unitPrice: 0 }]);
  }

  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
        <h2 className="card-title" style={{ margin: 0 }}>Place New Order</h2>
        {isLoading && (
          <span style={{ fontSize: '0.8rem', color: 'var(--accent-cyan)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
            <span className="spinner" /> In-Flight Transaction...
          </span>
        )}
      </div>

      {/* ------------------------------------------------------------------ */}
      {/* 1. Structured Error Display                                        */}
      {/* ------------------------------------------------------------------ */}
      {error && (
        <div className={`alert-box ${error.kind === 'DEGRADED' ? 'alert-warning' : 'alert-error'}`}>
          <div style={{ fontWeight: 600, marginBottom: '0.25rem' }}>
            {error.kind === 'VALIDATION' && '⚠️ Domain Invariant Violation (HTTP 400)'}
            {error.kind === 'DEGRADED' && '⚡ Service Temporarily Degraded (HTTP 503/500)'}
            {error.kind === 'NETWORK' && '🔌 Network Connection Failed'}
            {error.kind === 'UNKNOWN' && '❌ Unexpected Failure'}
          </div>
          <div>{error.message}</div>
          {error.details && (
            <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem' }}>
              {Array.isArray(error.details) ? (
                error.details.map((d, i) => <li key={i}>{d}</li>)
              ) : (
                <li>{error.details}</li>
              )}
            </ul>
          )}
        </div>
      )}

      {/* ------------------------------------------------------------------ */}
      {/* 2. Order Created Confirmation State                                */}
      {/* ------------------------------------------------------------------ */}
      {createdOrder ? (
        <div className="order-result-box" style={{ borderColor: 'rgba(16, 185, 129, 0.4)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--accent-success)', fontWeight: 600 }}>
            <span>✓</span> Order Created &amp; Outbox Staged Atomically
          </div>

          <div style={{ margin: '1rem 0' }}>
            <div className="metric-row">
              <span style={{ color: 'var(--text-secondary)' }}>Generated Order ID:</span>
              <code style={{ color: 'var(--accent-cyan)', fontWeight: 700 }}>{createdOrder.id}</code>
            </div>
            <div className="metric-row">
              <span style={{ color: 'var(--text-secondary)' }}>Customer Name:</span>
              <span>{createdOrder.customerName}</span>
            </div>
            <div className="metric-row">
              <span style={{ color: 'var(--text-secondary)' }}>Total Amount:</span>
              <strong style={{ color: 'var(--text-primary)' }}>${createdOrder.totalAmount.toFixed(2)}</strong>
            </div>
            <div className="metric-row">
              <span style={{ color: 'var(--text-secondary)' }}>Items Count:</span>
              <span>{createdOrder.items.length} line item(s)</span>
            </div>
          </div>

          <button type="button" className="btn btn-secondary" style={{ width: '100%' }} onClick={handleStartNew}>
            Place Another Order
          </button>
        </div>
      ) : (
        /* ------------------------------------------------------------------ */
        /* 3. Form Input State (Disabled when in-flight)                      */
        /* ------------------------------------------------------------------ */
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="customerName">Customer Name</label>
            <input
              id="customerName"
              type="text"
              placeholder="e.g. Acme Corp or Alice"
              value={customerName}
              disabled={isLoading}
              onChange={(e) => setCustomerName(e.target.value)}
            />
          </div>

          <div className="items-section">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
              <label style={{ margin: 0 }}>Line Items</label>
              <button
                type="button"
                className="btn btn-secondary"
                style={{ padding: '0.3rem 0.6rem', fontSize: '0.75rem' }}
                disabled={isLoading}
                onClick={handleAddItem}
              >
                + Add Item
              </button>
            </div>

            {items.map((item, idx) => (
              <div key={idx} className="item-row">
                <input
                  type="text"
                  placeholder="Product name"
                  value={item.productName}
                  disabled={isLoading}
                  onChange={(e) => handleUpdateItem(idx, 'productName', e.target.value)}
                />
                <input
                  type="number"
                  min="1"
                  placeholder="Qty"
                  value={item.quantity}
                  disabled={isLoading}
                  onChange={(e) => handleUpdateItem(idx, 'quantity', parseInt(e.target.value) || 0)}
                />
                <div style={{ display: 'flex', gap: '0.35rem' }}>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Price"
                    value={item.unitPrice}
                    disabled={isLoading}
                    onChange={(e) => handleUpdateItem(idx, 'unitPrice', parseFloat(e.target.value) || 0)}
                  />
                  {items.length > 1 && (
                    <button
                      type="button"
                      disabled={isLoading}
                      onClick={() => handleRemoveItem(idx)}
                      style={{
                        background: 'transparent',
                        border: 'none',
                        color: 'var(--accent-danger)',
                        cursor: 'pointer',
                        padding: '0 0.5rem',
                        fontSize: '1rem',
                      }}
                      title="Remove Item"
                    >
                      ✕
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            style={{ width: '100%', position: 'relative' }}
            disabled={isLoading}
          >
            {isLoading ? (
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem' }}>
                <span className="spinner" /> Submitting Transaction...
              </span>
            ) : (
              'Submit Order'
            )}
          </button>
        </form>
      )}
    </div>
  );
}
