import type { MenuDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const menu: MenuDto = {
  halfAndHalfPricing: 'MoreExpensiveHalf',
  categories: [{ id: 'pizza', name: 'Pizza', sizes: [{ id: 's32', name: '32 cm' }, { id: 's42', name: '42 cm' }] }],
  products: [
    { id: 'margherita', categoryId: 'pizza', name: 'Margherita', description: null, isActive: true, allowsHalves: true,
      prices: [{ sizeId: 's32', price: 25 }, { sizeId: 's42', price: 32 }], modifierGroupIds: ['sauce'] },
    { id: 'old', categoryId: 'pizza', name: 'Wycofana', description: null, isActive: false, allowsHalves: false,
      prices: [{ sizeId: 's32', price: 20 }], modifierGroupIds: [] },
  ],
  modifierGroups: [
    { id: 'sauce', categoryId: 'pizza', name: 'Sos', minSelect: 1, maxSelect: 1, modifiers: [
      { id: 'garlic', name: 'Czosnkowy', isActive: true, prices: [{ sizeId: 's32', price: 2 }, { sizeId: 's42', price: 2 }] },
    ] },
  ],
};

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('phone order', () => {
  test('staff types the order while talking and places it for pickup', async () => {
    const { bodiesOf, router } = renderApp({
      path: '/orders/new',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['Staff'], locationIds: ['l-1'] })),
        'GET /api/menu': json(menu),
        'GET /api/locations': json([]),
        'GET /api/locations/l-1/orders': json([]),
        'POST /api/locations/l-1/orders': json({ id: 'o-9', number: 9, trackingToken: 't', total: 54 }, 201),
      },
    });

    await screen.findByLabelText('Telefon klienta');
    type('Telefon klienta', '600 700 800');
    type('Imię klienta', 'Pan Zenon');
    fireEvent.click(screen.getByRole('button', { name: 'Odbiór w tym lokalu' }));
    expect(screen.queryByRole('option', { name: 'Wycofana' })).toBeNull();

    type('Produkt', 'margherita');
    type('Rozmiar', 's42');
    const add = screen.getByRole('button', { name: 'Dodaj pozycję' }) as HTMLButtonElement;
    expect(add.disabled).toBe(true); // the sauce is required
    fireEvent.click(screen.getByLabelText('Czosnkowy'));
    type('Uwagi do pozycji', 'dobrze wypieczona');
    fireEvent.keyDown(screen.getByLabelText('Uwagi do pozycji'), { key: 'Enter' }); // Enter adds the item
    expect(screen.getByRole('button', { name: /Złóż zamówienie · 34,00\s*zł/ })).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /Złóż zamówienie/ }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/orders'));
    expect(await bodiesOf('POST /api/locations/l-1/orders')).toEqual([
      {
        fulfillment: 'Pickup',
        pickupLocationId: 'l-1',
        deliveryAddress: null,
        customer: { name: 'Pan Zenon', phone: '600 700 800' },
        paymentMethod: 'Cash',
        notes: null,
        items: [{ productId: 'margherita', secondHalfProductId: null, sizeId: 's42', quantity: 1, modifierIds: ['garlic'], notes: 'dobrze wypieczona' }],
      },
    ]);
  });

  test('an order without items is not sent', async () => {
    const { bodiesOf } = renderApp({
      path: '/orders/new',
      signedIn: true,
      routes: { 'GET /api/auth/me': json(user({ roles: ['Staff'], locationIds: ['l-1'] })), 'GET /api/menu': json(menu) },
    });

    fireEvent.click(await screen.findByRole('button', { name: /Złóż zamówienie/ }));

    expect(await screen.findByText('Dodaj co najmniej jedną pozycję.')).toBeTruthy();
    expect(await bodiesOf('POST /api/locations/l-1/orders')).toEqual([]);
  });
});
