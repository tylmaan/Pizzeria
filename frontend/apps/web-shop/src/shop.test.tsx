import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import type { CartItem } from './cart/cart';
import { json, renderShop } from './test/fixtures';

const pizzaInCart: CartItem = { key: 'k1', productId: 'margherita', secondHalfProductId: null, sizeId: 's42', modifierIds: ['garlic'], quantity: 2 };

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('shop', () => {
  test('menu lists products with their lowest price', async () => {
    renderShop({});

    expect(await screen.findByRole('heading', { name: 'Pizza' })).toBeTruthy();
    expect(screen.getByText('Margherita')).toBeTruthy();
    expect(screen.getByText(/od 25,00\s*zł/)).toBeTruthy();
  });

  test('a product with a required sauce cannot be added until the sauce is chosen', async () => {
    const { cart } = renderShop({ path: '/product/margherita' });

    const add = await screen.findByRole('button', { name: /Dodaj do koszyka/ });
    expect((add as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click(screen.getByLabelText(/42 cm/));
    fireEvent.click(screen.getByLabelText(/Pomidorowy/));
    fireEvent.click(screen.getByLabelText(/^Ser/));
    fireEvent.click(screen.getByRole('button', { name: 'Więcej' }));
    expect(screen.getByRole('button', { name: /Dodaj do koszyka · 82,00\s*zł/ })).toBeTruthy(); // 2 × (32 + 3 + 6)
    fireEvent.click(screen.getByRole('button', { name: /Dodaj do koszyka/ }));

    expect(await screen.findByRole('link', { name: /Koszyk \(2\) · 82,00\s*zł/ })).toBeTruthy();
    expect(cart()).toMatchObject([{ productId: 'margherita', sizeId: 's42', modifierIds: ['tomato', 'cheese'], quantity: 2 }]);
  });

  test('half-and-half offers only shared modifier groups and prices by the company rule', async () => {
    renderShop({ path: '/product/margherita' });

    fireEvent.click(await screen.findByLabelText('Pół na pół'));
    type('Druga połowa', 'pepperoni');

    expect(screen.queryByLabelText(/^Ser/)).toBeNull();
    fireEvent.click(screen.getByLabelText(/Czosnkowy/));
    expect(screen.getByRole('button', { name: /Dodaj do koszyka · 32,00\s*zł/ })).toBeTruthy(); // max(25, 30) + 2
  });

  test('checkout sends the order without any prices and opens the tracking page', async () => {
    const { bodiesOf, router, cart } = renderShop({
      path: '/checkout',
      cart: [pizzaInCart],
      routes: {
        'POST /api/delivery/check': json({
          delivers: true, point: { latitude: 53.1, longitude: 23.1 }, formattedAddress: 'x', locationId: 'l-1', locationName: 'Centrum',
          zoneId: 'z', zoneName: 'Centrum', deliveryFee: 6, minOrderValue: 40, estimatedMinutes: 40,
        }),
        'POST /api/shop/orders': json({ id: 'o-1', number: 7, trackingToken: 'secret-token', total: 74 }, 201),
        'GET /api/shop/orders/secret-token': json({
          number: 7, status: 'New', fulfillment: 'Delivery', locationName: 'Centrum', placedAt: '2026-11-26T18:00:00Z',
          items: [], itemsTotal: 68, deliveryFee: 6, total: 74, paymentMethod: 'Cash', paymentStatus: 'Unpaid',
          deliveryAddress: null, cancellationReason: null, history: [],
        }),
      },
    });

    await screen.findByLabelText('Ulica i numer');
    type('Ulica i numer', 'ul. Rynek 1');
    type('Kod pocztowy', '15-001');
    type('Miasto', 'Białystok');
    fireEvent.click(screen.getByRole('button', { name: 'Sprawdź adres' }));
    expect(await screen.findByText(/Dowozimy z lokalu Centrum: dostawa 6,00\s*zł/)).toBeTruthy();
    type('Imię', 'Jan');
    type('Telefon', '600 100 200');
    fireEvent.click(screen.getByRole('button', { name: /Zamawiam/ }));

    expect(await screen.findByRole('heading', { name: 'Zamówienie nr 7' })).toBeTruthy();
    expect(router.state.location.pathname).toBe('/order/secret-token');
    expect(cart()).toEqual([]);
    expect(await bodiesOf('POST /api/shop/orders')).toEqual([
      {
        fulfillment: 'Delivery',
        pickupLocationId: null,
        deliveryAddress: { street: 'ul. Rynek 1', postalCode: '15-001', city: 'Białystok', notes: null },
        customer: { name: 'Jan', phone: '600 100 200' },
        paymentMethod: 'Cash',
        notes: null,
        items: [{ productId: 'margherita', secondHalfProductId: null, sizeId: 's42', quantity: 2, modifierIds: ['garlic'], notes: null }],
      },
    ]);
  });

  test('pickup needs no address and a rejected order shows the translated reason', async () => {
    const { bodiesOf } = renderShop({
      path: '/checkout',
      cart: [pizzaInCart],
      routes: { 'POST /api/shop/orders': json({ status: 422, code: 'menu.product_unavailable' }, 422) },
    });

    fireEvent.click(await screen.findByLabelText('Odbiór osobisty'));
    type('Imię', 'Anna');
    type('Telefon', '600100200');
    fireEvent.click(screen.getByLabelText('Karta przy odbiorze'));
    fireEvent.click(screen.getByRole('button', { name: /Zamawiam/ }));

    expect(await screen.findByText('Produkt jest niedostępny.')).toBeTruthy();
    expect(await bodiesOf('POST /api/shop/orders')).toMatchObject([
      { fulfillment: 'Pickup', pickupLocationId: 'l-1', deliveryAddress: null, paymentMethod: 'CardOnDelivery' },
    ]);
  });

  test('tracking shows progress of a delivery', async () => {
    renderShop({
      path: '/order/t',
      routes: {
        'GET /api/shop/orders/t': json({
          number: 3, status: 'InDelivery', fulfillment: 'Delivery', locationName: 'Centrum', placedAt: '2026-11-26T18:00:00Z',
          items: [{ productName: 'Margherita', secondHalfProductName: 'Pepperoni', sizeName: '42 cm', quantity: 1, unitPrice: 40, lineTotal: 40, modifiers: [{ name: 'Czosnkowy', price: 2 }], notes: null }],
          itemsTotal: 40, deliveryFee: 6, total: 46, paymentMethod: 'CardOnDelivery', paymentStatus: 'Unpaid',
          deliveryAddress: null, cancellationReason: null, history: [],
        }),
      },
    });

    const current = await screen.findByText('W drodze');
    expect(current.closest('li')?.getAttribute('aria-current')).toBe('step');
    expect(screen.getByText(/Margherita/).textContent).toContain('/ Pepperoni');
    expect(screen.getByText('Karta przy odbiorze')).toBeTruthy();
  });

  test('the cart lets the customer change quantities and remove items', async () => {
    const { cart } = renderShop({ path: '/cart', cart: [pizzaInCart] });

    expect(await screen.findAllByText(/68,00\s*zł/)).toHaveLength(2); // line and total
    fireEvent.click(screen.getByRole('button', { name: 'Mniej' }));
    expect(cart()[0]?.quantity).toBe(1);
    fireEvent.click(screen.getByRole('button', { name: 'Usuń' }));

    expect(await screen.findByText('Koszyk jest pusty.')).toBeTruthy();
  });
});
