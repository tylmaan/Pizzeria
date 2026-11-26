import { fireEvent, screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import type { CartItem } from './cart/cart';
import { json, renderShop, shop } from './test/fixtures';

const pizza: CartItem = { key: 'k1', productId: 'margherita', secondHalfProductId: null, sizeId: 's32', modifierIds: ['garlic'], quantity: 1 };

const tracking = (paymentStatus: string) => () =>
  Response.json({
    number: 5, status: 'New', fulfillment: 'Pickup', locationName: 'Centrum', placedAt: '2026-11-26T18:00:00Z', items: [],
    itemsTotal: 27, deliveryFee: 0, total: 27, paymentMethod: 'Online', paymentStatus, deliveryAddress: null,
    cancellationReason: null, history: [],
  });

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('online payment', () => {
  test('is offered only when the company has a payment provider', async () => {
    renderShop({ path: '/checkout', cart: [pizza], routes: { 'GET /api/shop': json({ ...shop, onlinePayments: false }) } });

    await screen.findByLabelText('Gotówka');
    expect(screen.queryByLabelText('Płatność online')).toBeNull();
  });

  test('placing an order paid online sends the customer to the payment page', async () => {
    const { router, bodiesOf } = renderShop({
      path: '/checkout',
      cart: [pizza],
      routes: {
        'POST /api/shop/orders': json({ id: 'o-5', number: 5, trackingToken: 'tok', total: 27, paymentRedirectUrl: '/pay/fake/fake_1?amount=27.00&return=%2Forder%2Ftok' }, 201),
      },
    });

    fireEvent.click(await screen.findByLabelText('Odbiór osobisty'));
    type('Imię', 'Anna');
    type('Telefon', '600100200');
    fireEvent.click(screen.getByLabelText('Płatność online'));
    fireEvent.click(screen.getByRole('button', { name: /Zamawiam i płacę online · 27,00\s*zł/ }));

    expect(await screen.findByRole('heading', { name: 'Testowa płatność' })).toBeTruthy();
    expect(router.state.location.pathname).toBe('/pay/fake/fake_1');
    expect(await bodiesOf('POST /api/shop/orders')).toMatchObject([{ paymentMethod: 'Online' }]);
  });

  test('the fake payment page completes the payment and returns to tracking', async () => {
    const { router, bodiesOf } = renderShop({
      path: '/pay/fake/fake_1?amount=27.00&return=%2Forder%2Ftok',
      routes: {
        'POST /api/payments/fake/fake_1/complete': () => new Response(null, { status: 200 }),
        'GET /api/shop/orders/tok': tracking('Paid'),
      },
    });

    expect(await screen.findByText(/Kwota: 27,00\s*zł/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Zapłać' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/order/tok'));
    expect(await bodiesOf('POST /api/payments/fake/fake_1/complete')).toEqual([{ succeeded: true }]);
  });

  test('the payment page never returns to an address outside the shop', async () => {
    const { router } = renderShop({
      path: '/pay/fake/fake_1?amount=27.00&return=https%3A%2F%2Fevil.example',
      routes: { 'POST /api/payments/fake/fake_1/complete': () => new Response(null, { status: 200 }) },
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Odrzuć płatność' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });

  test('an unpaid online order can be paid again from the tracking page', async () => {
    const { router } = renderShop({
      path: '/order/tok',
      routes: {
        'GET /api/shop/orders/tok': tracking('PendingOnline'),
        'POST /api/shop/orders/tok/payment': json({ redirectUrl: '/pay/fake/fake_2?amount=27.00&return=%2Forder%2Ftok' }),
      },
    });

    expect(await screen.findByText('Płatność online nie została jeszcze potwierdzona.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Zapłać teraz' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/pay/fake/fake_2'));
  });
});
