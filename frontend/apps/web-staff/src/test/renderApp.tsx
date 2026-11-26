import type { MeResponse, RefreshTokenStore } from '@pizzeria/api-client';
import { act, render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import type { FeedState, OrderChange } from '../app/ordersFeed';
import { AppServicesProvider, createAppServices } from '../app/services';
import { routes } from '../routes';

type Handler = (request: Request) => Response | Promise<Response>;

/** Fake backend keyed by `"METHOD /path"`; unknown routes answer 404 so a missing stub fails loudly. */
export type FakeRoutes = Record<string, Handler>;

export const json = (body: unknown, status = 200) => () => Response.json(body, { status });

export const tokens = json({ tokenType: 'Bearer', accessToken: 'access', refreshToken: 'refresh', expiresIn: 1800 });

export function user(overrides: Partial<MeResponse> = {}): MeResponse {
  return {
    userId: 'u-1',
    email: 'owner@demo.local',
    displayName: 'Właściciel',
    companyId: 'c-1',
    roles: ['CompanyAdmin'],
    locationIds: [],
    ...overrides,
  };
}

export function renderApp({
  path = '/',
  signedIn = false,
  routes: fakeRoutes = {},
}: {
  path?: string;
  signedIn?: boolean;
  routes?: FakeRoutes;
}) {
  const requests: Request[] = [];
  const handlers: FakeRoutes = {
    'POST /api/auth/login': tokens,
    'POST /api/auth/refresh': tokens,
    ...fakeRoutes,
  };

  const fetch = async (request: Request): Promise<Response> => {
    requests.push(request.clone());
    const key = `${request.method} ${new URL(request.url).pathname}`;
    const handler = handlers[key];
    return handler ? handler(request) : Response.json({ code: 'common.not_found' }, { status: 404 });
  };

  let stored: string | null = signedIn ? 'refresh' : null;
  const store: RefreshTokenStore = { load: () => stored, save: (token) => (stored = token) };

  // Fake live feed: tests push order changes and connection state by hand.
  const feed = {
    alerts: 0,
    listeners: [] as { onChange: (change: OrderChange | null) => void; onState: (state: FeedState) => void }[],
    emit(change: OrderChange | null) {
      act(() => feed.listeners.forEach((l) => l.onChange(change)));
    },
  };
  const services = createAppServices({
    fetch,
    store,
    ordersFeed: (_locationId, onChange, onState) => {
      const listener = { onChange, onState };
      feed.listeners.push(listener);
      queueMicrotask(() => onState('connected'));
      return () => feed.listeners.splice(feed.listeners.indexOf(listener), 1);
    },
    newOrderAlert: () => {
      feed.alerts++;
    },
  });
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <AppServicesProvider services={services}>
      <RouterProvider router={router} />
    </AppServicesProvider>,
  );

  /** Parsed JSON bodies of requests sent to `"METHOD /path"`. */
  const bodiesOf = (route: string) =>
    Promise.all(
      requests
        .filter((r) => `${r.method} ${new URL(r.url).pathname}` === route)
        .map(async (r) => {
          const text = await r.text();
          return (text === '' ? null : JSON.parse(text)) as unknown;
        }),
    );

  return { router, services, bodiesOf, feed, requests };
}
