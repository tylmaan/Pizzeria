import { ApiError, createApiClient, unwrap, type ApiClient, type DeliveryCheckRequest, type PlaceOrderRequest } from '@pizzeria/api-client';
import { QueryClient, QueryClientProvider, useMutation, useQuery } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { CartProvider, localStorageCartStore, type CartStore } from '../cart/cart';
import { signalRTrackingFeed, type TrackingFeed } from './trackingFeed';

export interface ShopServices {
  api: ApiClient;
  queryClient: QueryClient;
  cartStore: CartStore;
  trackingFeed: TrackingFeed;
}

export function createShopServices(
  options: { fetch?: (request: Request) => Promise<Response>; cartStore?: CartStore; trackingFeed?: TrackingFeed } = {},
): ShopServices {
  return {
    // Anonymous: the API recognises the shop by the host name.
    api: createApiClient(options.fetch ? { fetch: options.fetch } : {}),
    queryClient: new QueryClient({
      defaultOptions: {
        queries: {
          retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 2,
        },
      },
    }),
    cartStore: options.cartStore ?? localStorageCartStore(),
    trackingFeed: options.trackingFeed ?? signalRTrackingFeed,
  };
}

const ServicesContext = createContext<ShopServices | null>(null);

export function ShopServicesProvider({ services, children }: { services: ShopServices; children: ReactNode }) {
  return (
    <ServicesContext.Provider value={services}>
      <QueryClientProvider client={services.queryClient}>
        <CartProvider store={services.cartStore}>{children}</CartProvider>
      </QueryClientProvider>
    </ServicesContext.Provider>
  );
}

function useServices(): ShopServices {
  const services = useContext(ServicesContext);
  if (services === null) {
    throw new Error('Shop services are missing.');
  }
  return services;
}

const useApi = (): ApiClient => useServices().api;

export function useShop() {
  const api = useApi();
  return useQuery({ queryKey: ['shop'], queryFn: () => unwrap(api.GET('/api/shop')), staleTime: 5 * 60_000 });
}

export function useDeliveryCheck() {
  const api = useApi();
  return useMutation({ mutationFn: (body: DeliveryCheckRequest) => unwrap(api.POST('/api/delivery/check', { body })) });
}

export function usePlaceOrder() {
  const api = useApi();
  return useMutation({ mutationFn: (body: PlaceOrderRequest) => unwrap(api.POST('/api/shop/orders', { body })) });
}

export function useRetryPayment(token: string) {
  const api = useApi();
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/shop/orders/{token}/payment', { params: { path: { token } } })),
  });
}

/** Development fake operator: completes a payment as the provider would (signed webhook on the server). */
export function useCompleteFakePayment(providerPaymentId: string) {
  const api = useApi();
  return useMutation({
    mutationFn: async (succeeded: boolean) => {
      const { response } = await api.POST('/api/payments/fake/{providerPaymentId}/complete', {
        params: { path: { providerPaymentId } },
        body: { succeeded },
      });
      if (!response.ok) {
        throw new Error(`Fake payment failed: ${response.status}`);
      }
    },
  });
}

/** Payment pages of real operators are external; the fake one is a page of this shop. */
export function goToPayment(url: string, navigate: (path: string) => unknown): void {
  if (url.startsWith('/')) {
    void navigate(url);
  } else {
    window.location.assign(url);
  }
}

const finished = new Set(['Delivered', 'PickedUp', 'Cancelled']);

/** Live order status: pushed by SignalR, with polling as a fallback while the live connection is down. */
export function useTracking(token: string) {
  const { api, trackingFeed, queryClient } = useServices();
  const [live, setLive] = useState(false);

  useEffect(
    () => trackingFeed(token, () => void queryClient.invalidateQueries({ queryKey: ['tracking', token] }), setLive),
    [token, trackingFeed, queryClient],
  );

  return useQuery({
    queryKey: ['tracking', token],
    queryFn: () => unwrap(api.GET('/api/shop/orders/{token}', { params: { path: { token } } })),
    refetchInterval: (query) => (live || (query.state.data && finished.has(query.state.data.status)) ? false : 15_000),
  });
}

/** The driver's position while the order is on its way (refreshed by the same live feed as the status). */
export function useDriverPosition(token: string, enabled: boolean) {
  const api = useApi();
  return useQuery({
    queryKey: ['tracking', token, 'driver'],
    queryFn: () => unwrap(api.GET('/api/shop/orders/{token}/driver', { params: { path: { token } } })),
    enabled,
    refetchInterval: 20_000,
  });
}
