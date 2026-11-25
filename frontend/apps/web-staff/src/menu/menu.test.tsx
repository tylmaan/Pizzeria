import type { MenuDto } from '@pizzeria/api-client';
import { fireEvent, screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const menu: MenuDto = {
  halfAndHalfPricing: 'MoreExpensiveHalf',
  categories: [
    {
      id: 'pizza',
      name: 'Pizza',
      sizes: [
        { id: 's32', name: '32 cm' },
        { id: 's42', name: '42 cm' },
      ],
    },
  ],
  products: [
    {
      id: 'margherita',
      categoryId: 'pizza',
      name: 'Margherita',
      description: 'Sos, ser',
      isActive: true,
      allowsHalves: true,
      prices: [
        { sizeId: 's32', price: 25.5 },
        { sizeId: 's42', price: 32 },
      ],
      modifierGroupIds: ['sauce'],
    },
  ],
  modifierGroups: [
    {
      id: 'sauce',
      categoryId: 'pizza',
      name: 'Sos',
      minSelect: 1,
      maxSelect: 1,
      modifiers: [{ id: 'garlic', name: 'Czosnkowy', isActive: true, prices: [{ sizeId: 's32', price: 2 }] }],
    },
  ],
};

const type = (label: string, value: string, container: HTMLElement = document.body) =>
  fireEvent.change(within(container).getByLabelText(label), { target: { value } });

function renderMenu(path: string, routes = {}) {
  return renderApp({
    path,
    signedIn: true,
    routes: { 'GET /api/auth/me': json(user()), 'GET /api/menu': json(menu), ...routes },
  });
}

describe('menu editor', () => {
  test('lists categories with sizes, products with formatted prices and modifier groups', async () => {
    renderMenu('/company/menu');

    expect(await screen.findByRole('heading', { name: 'Pizza' })).toBeTruthy();
    expect(screen.getByText('32 cm · 42 cm')).toBeTruthy();
    expect(screen.getByText(/32 cm: 25,50\s*zł · 42 cm: 32,00\s*zł/)).toBeTruthy();
    expect(screen.getByText('(wybór 1–1)')).toBeTruthy();
  });

  test('changing the half-and-half rule saves the company setting', async () => {
    const { bodiesOf } = renderMenu('/company/menu', {
      'PUT /api/menu/settings': json({ halfAndHalfPricing: 'AverageOfHalves' }),
    });

    fireEvent.change(await screen.findByLabelText('Cena pizzy pół na pół'), { target: { value: 'AverageOfHalves' } });

    expect(await screen.findByText('Zapisano.')).toBeTruthy();
    expect(await bodiesOf('PUT /api/menu/settings')).toEqual([{ halfAndHalfPricing: 'AverageOfHalves' }]);
  });

  test('editing a product sends prices with decimal commas parsed and skips sizes left empty', async () => {
    const { bodiesOf, router } = renderMenu('/company/menu/products/margherita', {
      'PUT /api/menu/products/margherita': json(menu.products[0]),
    });

    expect(await screen.findByDisplayValue('25,50')).toBeTruthy();
    type('32 cm', '26,9');
    type('42 cm', '');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    await screen.findByRole('heading', { name: 'Menu' });
    expect(router.state.location.pathname).toBe('/company/menu');
    expect(await bodiesOf('PUT /api/menu/products/margherita')).toEqual([
      {
        categoryId: 'pizza',
        name: 'Margherita',
        description: 'Sos, ser',
        allowsHalves: true,
        isActive: true,
        prices: [{ sizeId: 's32', price: 26.9 }],
        modifierGroupIds: ['sauce'],
      },
    ]);
  });

  test('a price that is not a number is marked locally and nothing is sent', async () => {
    const { bodiesOf } = renderMenu('/company/menu/products/new?category=pizza');

    await screen.findByLabelText('Nazwa produktu');
    type('Nazwa produktu', 'Hawajska');
    type('32 cm', 'dwadzieścia');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Cena: liczba ≥ 0, maks. 2 miejsca po przecinku.')).toBeTruthy();
    expect(await bodiesOf('POST /api/menu/products')).toEqual([]);
  });

  test('API price errors are shown under the size they refer to', async () => {
    renderMenu('/company/menu/products/new?category=pizza', {
      'POST /api/menu/products': json(
        { status: 400, code: 'common.validation_failed', errors: { 'prices[0].price': ['validation.price'] } },
        400,
      ),
    });

    await screen.findByLabelText('Nazwa produktu');
    type('Nazwa produktu', 'Hawajska');
    type('42 cm', '99999999999');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    const field42 = (await screen.findByText('Cena: liczba ≥ 0, maks. 2 miejsca po przecinku.')).closest('div')!;
    expect(within(field42).getByLabelText('42 cm')).toBeTruthy();
  });

  test('category form keeps ids of existing sizes and adds new ones', async () => {
    const { bodiesOf } = renderMenu('/company/menu/categories/pizza', {
      'PUT /api/menu/categories/pizza': json(menu.categories[0]),
    });

    await screen.findByDisplayValue('32 cm');
    fireEvent.click(screen.getByRole('button', { name: 'Dodaj rozmiar' }));
    type('Rozmiar 3', '50 cm');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    await screen.findByRole('heading', { name: 'Menu' });
    expect(await bodiesOf('PUT /api/menu/categories/pizza')).toEqual([
      {
        name: 'Pizza',
        sizes: [
          { id: 's32', name: '32 cm' },
          { id: 's42', name: '42 cm' },
          { id: null, name: '50 cm' },
        ],
      },
    ]);
  });

  test('new modifier group sends limits as numbers and a price per size', async () => {
    const { bodiesOf } = renderMenu('/company/menu/modifier-groups/new?category=pizza', {
      'POST /api/menu/modifier-groups': json(menu.modifierGroups[0], 201),
    });

    await screen.findByLabelText('Nazwa grupy');
    type('Nazwa grupy', 'Dodatki');
    type('Maksimum wyborów', '3');
    type('Nazwa dodatku', 'Ser');
    type('32 cm', '4');
    type('42 cm', '6,5');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    await screen.findByRole('heading', { name: 'Menu' });
    expect(await bodiesOf('POST /api/menu/modifier-groups')).toEqual([
      {
        categoryId: 'pizza',
        name: 'Dodatki',
        minSelect: 0,
        maxSelect: 3,
        modifiers: [
          {
            id: null,
            name: 'Ser',
            isActive: true,
            prices: [
              { sizeId: 's32', price: 4 },
              { sizeId: 's42', price: 6.5 },
            ],
          },
        ],
      },
    ]);
  });
});
