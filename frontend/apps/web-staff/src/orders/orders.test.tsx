import type { StaffOrderDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const staff = user({ roles: ['Staff'], locationIds: ['l-1'], displayName: 'Obsługa' });

function order(overrides: Partial<StaffOrderDto>): StaffOrderDto {
  return {
    id: 'o-1', locationId: 'l-1', number: 12, status: 'New', nextStatus: 'Accepted', fulfillment: 'Delivery', source: 'Online',
    placedAt: new Date(Date.now() - 5 * 60_000).toISOString(), acceptedAt: null, statusChangedAt: new Date().toISOString(),
    customerName: 'Jan Kowalski', customerPhone: '600 100 200',
    deliveryAddress: { street: 'ul. Rynek 1', postalCode: '15-001', city: 'Białystok' }, deliveryNotes: 'domofon 5', deliveryPoint: null,
    notes: null,
    items: [{ productName: 'Margherita', secondHalfProductName: null, sizeName: '42 cm', quantity: 2, unitPrice: 34, lineTotal: 68, modifiers: [{ name: 'Czosnkowy', price: 2 }], notes: 'bez oregano' }],
    itemsTotal: 68, deliveryFee: 6, total: 74, paymentMethod: 'Cash', paymentStatus: 'Unpaid', driverId: null, cancellationReason: null,
    ...overrides,
  };
}

function renderQueue(orders: StaffOrderDto[], routes = {}) {
  let current = orders;
  const app = renderApp({
    path: '/orders',
    signedIn: true,
    routes: {
      'GET /api/auth/me': json(staff),
      'GET /api/locations': json([]),
      'GET /api/locations/l-1/orders': () => Response.json(current),
      ...routes,
    },
  });
  return { ...app, setOrders: (next: StaffOrderDto[]) => (current = next) };
}

describe('order queue', () => {
  test('groups orders by stage and shows what staff needs at a glance', async () => {
    renderQueue([order({}), order({ id: 'o-2', number: 13, status: 'InPreparation', nextStatus: 'Ready', fulfillment: 'Pickup', deliveryAddress: null })]);

    const newColumn = await screen.findByRole('region', { name: 'Nowe' });
    const card = await within(newColumn).findByRole('article', { name: '#12' });
    expect(within(card).getByText('5 min')).toBeTruthy();
    expect(within(card).getByRole('link', { name: '600 100 200' }).getAttribute('href')).toBe('tel:600100200');
    expect(within(card).getByText('bez oregano')).toBeTruthy();
    expect(within(card).getByRole('button', { name: 'Przyjmij' })).toBeTruthy();
    expect(within(screen.getByRole('region', { name: 'W realizacji' })).getByRole('button', { name: 'Gotowe' })).toBeTruthy();
    expect(screen.getByText('Na żywo')).toBeTruthy();
  });

  test('one tap moves the order to the next status', async () => {
    const { bodiesOf, setOrders } = renderQueue([order({})], {
      'POST /api/orders/o-1/advance': () => {
        setOrders([order({ status: 'Accepted', nextStatus: 'InPreparation' })]);
        return Response.json(order({ status: 'Accepted', nextStatus: 'InPreparation' }));
      },
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Przyjmij' }));

    const inProgress = screen.getByRole('region', { name: 'W realizacji' });
    expect(await within(inProgress).findByRole('button', { name: 'Do przygotowania' })).toBeTruthy();
    expect(await bodiesOf('POST /api/orders/o-1/advance')).toEqual([null]);
  });

  test('a new order pushed live appears and rings', async () => {
    const { feed, setOrders } = renderQueue([]);
    await screen.findAllByText('Brak zamówień.');

    setOrders([order({ number: 99 })]);
    feed.emit({ orderId: 'o-1', locationId: 'l-1', number: 99, status: 'New' });

    expect(await screen.findByRole('article', { name: '#99' })).toBeTruthy();
    expect(feed.alerts).toBe(1);
  });

  test('cancelling asks for a reason', async () => {
    const { bodiesOf } = renderQueue([order({})], {
      'POST /api/orders/o-1/cancel': json(order({ status: 'Cancelled', nextStatus: null })),
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Anuluj' }));
    const confirm = screen.getByRole('button', { name: 'Anuluj zamówienie' }) as HTMLButtonElement;
    expect(confirm.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText('Powód anulowania'), { target: { value: 'Klient zrezygnował' } });
    fireEvent.click(confirm);

    await waitFor(async () => expect(await bodiesOf('POST /api/orders/o-1/cancel')).toEqual([{ reason: 'Klient zrezygnował' }]));
  });

  test('a ready delivery is handed to a driver of the location', async () => {
    const { bodiesOf } = renderQueue([order({ status: 'Ready', nextStatus: null })], {
      'GET /api/locations/l-1/drivers': json([{ id: 'd-1', displayName: 'Marek' }]),
      'POST /api/orders/o-1/driver': json(order({ status: 'AssignedToDriver', nextStatus: 'InDelivery', driverId: 'd-1' })),
    });

    const card = await screen.findByRole('article', { name: '#12' });
    await within(card).findByRole('option', { name: 'Marek' });
    fireEvent.change(within(card).getByLabelText('Kierowca'), { target: { value: 'd-1' } });
    fireEvent.click(within(card).getByRole('button', { name: 'Przypisz kierowcę' }));

    await waitFor(async () => expect(await bodiesOf('POST /api/orders/o-1/driver')).toEqual([{ driverId: 'd-1' }]));
  });

  test('pressing N opens a new phone order', async () => {
    const { router } = renderQueue([]);
    await screen.findAllByText('Brak zamówień.');

    fireEvent.keyDown(window, { key: 'n' });

    expect(router.state.location.pathname).toBe('/orders/new');
  });
});
