export type OrderType = 'market' | 'limit' | 'stop-loss' | 'take-profit' | 'trailing-stop';
export type OrderStatus = 'pending' | 'filled' | 'rejected';

export interface PendingOrder {
  id: string;
  ticker: string;
  action: 'buy' | 'sell';
  quantity: number;
  price: number;
  orderType: OrderType;
  status: OrderStatus;
  reason: string;
  strategy: string;
  createdAt: Date;
  estimatedFillTime?: Date;
}

/**
 * Simulate order fill time based on order type and market conditions.
 * - Market orders: 10-20 seconds
 * - Limit orders ITM: 10-30 seconds
 * - Limit orders OTM: pending until price reaches bounds or 1 minute passes
 */
export function simulateOrderFillTime(
  orderType: OrderType,
  currentPrice: number,
  orderPrice: number,
  action: 'buy' | 'sell',
  resistanceLow: number,
  resistanceHigh: number,
): { fillTimeMs: number; status: OrderStatus } {
  // Market orders always fill: 10-20 seconds
  if (orderType === 'market') {
    const fillTimeMs = (10 + Math.random() * 10) * 1000;
    return { fillTimeMs, status: 'filled' };
  }

  if (orderType === 'trailing-stop') {
    const fillTimeMs = (25 + Math.random() * 35) * 1000;
    return { fillTimeMs, status: 'filled' };
  }

  if (orderType === 'stop-loss') {
    const triggerReached = action === 'buy' ? currentPrice >= orderPrice : currentPrice <= orderPrice;
    if (triggerReached) {
      const fillTimeMs = (8 + Math.random() * 12) * 1000;
      return { fillTimeMs, status: 'filled' };
    }

    return { fillTimeMs: Infinity, status: 'pending' };
  }

  if (orderType === 'take-profit') {
    const triggerReached = action === 'buy' ? currentPrice <= orderPrice : currentPrice >= orderPrice;
    if (triggerReached) {
      const fillTimeMs = (8 + Math.random() * 12) * 1000;
      return { fillTimeMs, status: 'filled' };
    }

    return { fillTimeMs: Infinity, status: 'pending' };
  }

  // Limit order logic
  // Buy order: ITM if limit price >= current price
  // Sell order: ITM if limit price <= current price
  const isItm = action === 'buy' ? orderPrice >= currentPrice : orderPrice <= currentPrice;

  if (isItm) {
    // ITM limit order: fill in 10-30 seconds
    const fillTimeMs = (10 + Math.random() * 20) * 1000;
    return { fillTimeMs, status: 'filled' };
  }

  // OTM limit order: check if within resistance bounds
  const withinBounds =
    orderPrice >= resistanceLow &&
    orderPrice <= resistanceHigh;

  if (withinBounds) {
    // Within bounds: fill in 30-60 seconds
    const fillTimeMs = (30 + Math.random() * 30) * 1000;
    return { fillTimeMs, status: 'filled' };
  }

  // Outside bounds: remains pending indefinitely (or until manually cancelled)
  return { fillTimeMs: Infinity, status: 'pending' };
}

/**
 * Format pending order display string
 */
export function formatPendingOrder(order: PendingOrder): string {
  const action = order.action.toUpperCase();
  const status = order.status === 'filled' ? '✓ Filled' : '⏳ Pending';
  const timeStr = order.estimatedFillTime
    ? `~${Math.round((order.estimatedFillTime.getTime() - Date.now()) / 1000)}s`
    : 'Waiting...';

  return `${action} ${order.quantity.toFixed(4)} ${order.ticker} @ ${order.price.toFixed(2)} — ${status} ${timeStr}`;
}
