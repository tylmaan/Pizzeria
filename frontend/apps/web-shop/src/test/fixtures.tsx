import type { MenuDto, ShopDto } from '@pizzeria/api-client';
import { act, render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { createShopServices, ShopServicesProvider } from '../app/services';
import type { CartItem, CartStore } from '../cart/cart';
import { routes } from '../routes';

export const menu: MenuDto = {
  halfAndHalfPricing: 'MoreExpensiveHalf',
  categories: [{ id: 'pizza', name: 'Pizza', sizes: [{ id: 's32', name: '32 cm' }, { id: 's42', name: '42 cm' }] }],
  products: [
    {
      id: 'margherita', categoryId: 'pizza', name: 'Margherita', description: 'Sos, ser', isActive: true, allowsHalves: true,
      prices: [{ sizeId: 's32', price: 25 }, { sizeId: 's42', price: 32 }], modifierGroupIds: ['sauce', 'toppings'],
    },
    {
      id: 'pepperoni', categoryId: 'pizza', name: 'Pepperoni', description: null, isActive: true, allowsHalves: true,
      prices: [{ sizeId: 's32', price: 30 }, { sizeId: 's42', price: 38 }], modifierGroupIds: ['sauce'],
    },
  ],
  modifierGroups: [
    {
      id: 'sauce', categoryId: 'pizza', name: 'Sos', minSelect: 1, maxSelect: 1,
      modifiers: [
        { id: 'garlic', name: 'Czosnkowy', isActive: true, prices: [{ sizeId: 's32', price: 2 }, { sizeId: 's42', price: 2 }] },
        { id: 'tomato', name: 'Pomidorowy', isActive: true, prices: [{ sizeId: 's32', price: 2 }, { sizeId: 's42', price: 3 }] },
      ],
    },
    {
      id: 'toppings', categoryId: 'pizza', name: 'Dodatki', minSelect: 0, maxSelect: 2,
      modifiers: [{ id: 'cheese', name: 'Ser', isActive: true, prices: [{ sizeId: 's32', price: 4 }, { sizeId: 's42', price: 6 }] }],
    },
  ],
};

export const shop: ShopDto = {
  companyName: 'Demo Pizza',
  locations: [{ id: 'l-1', name: 'Centrum', address: { street: 'ul. Lipowa 1', postalCode: '15-001', city: 'Białystok' } }],
  menu,
};

export const json = (body: unknown, status = 200) => () => Response.json(body, { status });

type Handler = (request: Request) => Response | Promise<Response>;

export function renderShop({ path = '/', routes: fake = {}, cart = [] }: { path?: string; routes?: Record<string, Handler>; cart?: CartItem[] }) {
  const requests: Request[] = [];
  const handlers: Record<string, Handler> = { 'GET /api/shop': json(shop), ...fake };
  const fetch = async (request: Request) => {
    requests.push(request.clone());
    const handler = handlers[`${request.method} ${new URL(request.url).pathname}`];
    return handler ? handler(request) : Response.json({ code: 'common.not_found' }, { status: 404 });
  };

  let saved = cart;
  const cartStore: CartStore = { load: () => saved, save: (items) => (saved = items) };
  const tracking: { push: () => void } = { push: () => undefined };
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <ShopServicesProvider services={createShopServices({ fetch, cartStore, trackingFeed: (_token, onChange) => { tracking.push = onChange; return () => undefined; } })}>
      <RouterProvider router={router} />
    </ShopServicesProvider>,
  );

  return {
    router,
    cart: () => saved,
    /** Simulates a live "order changed" push. */
    pushTracking: () => act(() => tracking.push()),
    bodiesOf: (route: string) =>
      Promise.all(requests.filter((r) => `${r.method} ${new URL(r.url).pathname}` === route).map((r) => r.json() as Promise<unknown>)),
  };
}
