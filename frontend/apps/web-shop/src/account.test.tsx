import type { CustomerOrderDto, SavedAddressDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import type { CartItem } from './cart/cart';
import { json, renderShop, tokens } from './test/fixtures';

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });
const typeNth = (label: string, index: number, value: string) =>
  fireEvent.change(screen.getAllByLabelText(label)[index]!, { target: { value } });

const profile = { email: 'anna@example.com', name: 'Anna', phone: '600100200' };
const home: SavedAddressDto = { id: 'a-1', label: 'Dom', street: 'ul. Lipowa 5', postalCode: '15-001', city: 'Białystok', notes: 'domofon 5' };
const order: CustomerOrderDto = {
  id: 'o-1', number: 12, placedAt: '2026-11-20T18:00:00Z', status: 'Delivered', fulfillment: 'Delivery', total: 74,
  trackingToken: 'secret-token', items: ['2× Margherita 42 cm'],
};
const account = {
  'GET /api/shop/account': json(profile),
  'GET /api/shop/account/addresses': json([home]),
  'GET /api/shop/account/orders': json([order]),
};
const pizzaInCart: CartItem = { key: 'k1', productId: 'margherita', secondHalfProductId: null, sizeId: 's42', modifierIds: ['garlic'], quantity: 1 };

describe('customer account', () => {
  test('signing in goes back to the checkout, with name, phone and saved addresses filled in', async () => {
    const { bodiesOf, signedIn } = renderShop({
      path: '/account/login?next=/checkout',
      cart: [pizzaInCart],
      routes: { 'POST /api/shop/account/login': tokens, ...account },
    });

    await screen.findByRole('heading', { name: 'Mam konto' });
    typeNth('E-mail', 0, 'anna@example.com');
    typeNth('Hasło', 0, 'tajne-haslo-1');
    fireEvent.click(screen.getByRole('button', { name: 'Zaloguj' }));

    expect(await screen.findByRole('heading', { name: 'Twoje zamówienie' })).toBeTruthy();
    expect(signedIn()).toBe(true);
    expect(await bodiesOf('POST /api/shop/account/login')).toEqual([{ email: 'anna@example.com', password: 'tajne-haslo-1' }]);
    await waitFor(() => expect((screen.getByLabelText('Imię') as HTMLInputElement).value).toBe('Anna'));
    expect((screen.getByLabelText('Telefon') as HTMLInputElement).value).toBe('600100200');

    fireEvent.change(await screen.findByLabelText('Zapisany adres'), { target: { value: 'a-1' } });
    expect((screen.getByLabelText('Ulica i numer') as HTMLInputElement).value).toBe('ul. Lipowa 5');
    expect(screen.queryByLabelText('Zapisz ten adres na koncie')).toBeNull(); // already saved
  });

  test('a new delivery address can be saved to the account with the order', async () => {
    const { bodiesOf, requests } = renderShop({
      path: '/checkout',
      signedIn: true,
      cart: [pizzaInCart],
      routes: {
        ...account,
        'GET /api/shop/account/addresses': json([]),
        'POST /api/shop/orders': json({ id: 'o-2', number: 13, trackingToken: 'other-token', total: 34 }, 201),
        'POST /api/shop/account/addresses': json({ ...home, id: 'a-2', label: null }, 201),
      },
    });

    await waitFor(() => expect((screen.getByLabelText('Imię') as HTMLInputElement).value).toBe('Anna'));
    type('Ulica i numer', 'ul. Nowa 1');
    type('Kod pocztowy', '15-002');
    type('Miasto', 'Białystok');
    fireEvent.click(screen.getByLabelText('Zapisz ten adres na koncie'));
    fireEvent.click(screen.getByRole('button', { name: /Zamawiam/ }));

    await waitFor(async () =>
      expect(await bodiesOf('POST /api/shop/account/addresses')).toEqual([
        { label: null, street: 'ul. Nowa 1', postalCode: '15-002', city: 'Białystok', notes: null },
      ]),
    );
    // The order goes out with the customer's token, so it lands in their history.
    const placed = requests.find((r) => r.method === 'POST' && new URL(r.url).pathname === '/api/shop/orders')!;
    expect(placed.headers.get('Authorization')).toBe('Bearer access');
  });

  test('the account shows order history with a link to the order, and signing out returns to guest', async () => {
    const { router, signedIn } = renderShop({ path: '/account', signedIn: true, routes: account });

    const item = await screen.findByRole('listitem', { name: '#12' });
    expect(within(item).getByText('2× Margherita 42 cm')).toBeTruthy();
    expect(within(item).getByText('Dostarczone')).toBeTruthy();
    expect(within(item).getByRole('link', { name: 'Szczegóły' }).getAttribute('href')).toBe('/order/secret-token');
    expect(await screen.findByText(/ul. Lipowa 5, 15-001 Białystok/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Wyloguj' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/account/login'));
    expect(signedIn()).toBe(false);
    expect(screen.getByRole('link', { name: 'Zaloguj się' })).toBeTruthy();
  });

  test('registration shows field errors from the API', async () => {
    renderShop({
      path: '/account/login',
      routes: {
        'POST /api/shop/account/register': json({ code: 'common.validation_failed', errors: { email: ['validation.email'] } }, 400),
      },
    });

    await screen.findByRole('heading', { name: 'Nowe konto' });
    type('Imię', 'Anna');
    typeNth('E-mail', 1, 'zly-email');
    typeNth('Hasło', 1, 'haslo-123');
    fireEvent.click(screen.getByRole('button', { name: 'Załóż konto' }));

    expect(await screen.findByText('Nieprawidłowy adres e-mail.')).toBeTruthy();
  });

  test('a link from outside the shop is never used as the place to go after signing in', async () => {
    const { router } = renderShop({
      path: '/account/login?next=//evil.example',
      routes: { 'POST /api/shop/account/login': tokens, ...account },
    });

    await screen.findByRole('heading', { name: 'Mam konto' });
    typeNth('E-mail', 0, 'anna@example.com');
    typeNth('Hasło', 0, 'tajne-haslo-1');
    fireEvent.click(screen.getByRole('button', { name: 'Zaloguj' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/account'));
  });
});
