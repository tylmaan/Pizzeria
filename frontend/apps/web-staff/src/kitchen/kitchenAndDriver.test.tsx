import type { StaffOrderDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { navigationUrl } from '../driver/DriverPage';
import { json, renderApp, user } from '../test/renderApp';
import { ageOf } from './KitchenPage';

const minutesAgo = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

function order(overrides: Partial<StaffOrderDto>): StaffOrderDto {
  return {
    id: 'o-1', locationId: 'l-1', number: 1, status: 'Accepted', nextStatus: 'InPreparation', fulfillment: 'Delivery', source: 'Online',
    placedAt: minutesAgo(30), acceptedAt: minutesAgo(5), statusChangedAt: minutesAgo(5),
    customerName: 'Jan', customerPhone: '600 100 200',
    deliveryAddress: { street: 'ul. Rynek 1', postalCode: '15-001', city: 'Białystok' }, deliveryNotes: 'domofon 5',
    deliveryPoint: { latitude: 53.13, longitude: 23.16 }, notes: null,
    items: [{ productName: 'Margherita', secondHalfProductName: null, sizeName: '42 cm', quantity: 2, unitPrice: 34, lineTotal: 68, modifiers: [], notes: 'bez oregano' }],
    itemsTotal: 68, deliveryFee: 6, total: 74, paymentMethod: 'Cash', paymentStatus: 'Unpaid', driverId: null, cancellationReason: null,
    ...overrides,
  };
}

describe('kitchen screen', () => {
  test('shows accepted orders oldest first with age colours and one-tap next step', async () => {
    const { bodiesOf, feed } = renderApp({
      path: '/kitchen',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['Kitchen'], locationIds: ['l-1'] })),
        'GET /api/locations/l-1/orders': json([
          order({ id: 'fresh', number: 3, acceptedAt: minutesAgo(2) }),
          order({ id: 'new', number: 4, status: 'New', acceptedAt: null }),
          order({ id: 'late', number: 1, status: 'InPreparation', nextStatus: 'Ready', acceptedAt: minutesAgo(25) }),
          order({ id: 'warn', number: 2, acceptedAt: minutesAgo(12) }),
        ]),
        'POST /api/orders/late/advance': json(order({ id: 'late', status: 'Ready', nextStatus: null })),
      },
    });

    const tiles = await screen.findAllByRole('listitem', { name: /^#\d+$/ });
    expect(tiles.map((t) => t.getAttribute('aria-label'))).toEqual(['#1', '#2', '#3']);
    expect(tiles.map((t) => t.dataset.age)).toEqual(['late', 'warning', 'fresh']);
    expect(within(tiles[0]!).getByText('! bez oregano')).toBeTruthy();

    fireEvent.click(within(tiles[0]!).getByRole('button', { name: 'Gotowe' }));
    await waitFor(async () => expect(await bodiesOf('POST /api/orders/late/advance')).toHaveLength(1));

    feed.emit({ orderId: 'x', locationId: 'l-1', number: 9, status: 'Accepted' });
    feed.emit({ orderId: 'y', locationId: 'l-1', number: 10, status: 'New' });
    expect(feed.alerts).toBe(1); // the kitchen rings only when an order reaches it
  });

  test('age thresholds', () => {
    expect([ageOf(0), ageOf(9), ageOf(10), ageOf(19), ageOf(20)]).toEqual(['fresh', 'fresh', 'warning', 'warning', 'late']);
  });
});

describe('driver app', () => {
  test('lists own runs, what to collect, and moves them on with big buttons', async () => {
    const { bodiesOf } = renderApp({
      path: '/driver',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['Driver'], locationIds: ['l-1'] })),
        'GET /api/locations/l-1/orders': json([
          order({ id: 'waiting', number: 5, status: 'AssignedToDriver', nextStatus: 'InDelivery', driverId: 'me' }),
          order({ id: 'going', number: 6, status: 'InDelivery', nextStatus: 'Delivered', driverId: 'me', paymentMethod: 'CardOnDelivery' }),
        ]),
        'POST /api/orders/going/advance': json(order({ id: 'going', status: 'Delivered', nextStatus: null })),
      },
    });

    const runs = await screen.findAllByRole('article');
    expect(runs.map((r) => r.getAttribute('aria-label'))).toEqual(['#6', '#5']); // on the way first
    const going = runs[0]!;
    expect(within(going).getByText(/Do pobrania: 74,00\s*zł \(Karta przy odbiorze\)/)).toBeTruthy();
    expect(within(going).getByRole('link', { name: 'Zadzwoń' }).getAttribute('href')).toBe('tel:600100200');
    expect(within(runs[1]!).getByRole('button', { name: 'Odebrałem z lokalu' })).toBeTruthy();

    fireEvent.click(within(going).getByRole('button', { name: 'Dostarczone' }));
    await waitFor(async () => expect(await bodiesOf('POST /api/orders/going/advance')).toHaveLength(1));
  });

  test('navigation uses coordinates when known, the address otherwise', () => {
    expect(navigationUrl(order({}))).toBe('https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=53.13%2C23.16');
    expect(navigationUrl(order({ deliveryPoint: null }))).toContain(encodeURIComponent('ul. Rynek 1, 15-001 Białystok'));
  });
});
