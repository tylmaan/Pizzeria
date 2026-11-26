import { ApiError, createApiClient, unwrap, type ApiClient, type DeliveryCheckRequest, type PlaceOrderRequest } from '@pizzeria/api-client';
import { QueryClient, QueryClientProvider, useMutation, useQuery } from '@tanstack/react-query';
import { createContext, useContext, type ReactNode } from 'react';
import { CartProvider, localStorageCartStore, type CartStore } from '../cart/cart';

export interface ShopServices {
  api: ApiClient;
  queryClient: QueryClient;
  cartStore: CartStore;
}

export function createShopServices(options: { fetch?: (request: Request) => Promise<Response>; cartStore?: CartStore } = {}): ShopServices {
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

function useApi(): ApiClient {
  const services = useContext(ServicesContext);
  if (services === null) {
    throw new Error('Shop services are missing.');
  }
  return services.api;
}

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

const finished = new Set(['Delivered', 'PickedUp', 'Cancelled']);

/** Polls until the order is finished. (Live push via SignalR comes with the staff order queue.) */
export function useTracking(token: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['tracking', token],
    queryFn: () => unwrap(api.GET('/api/shop/orders/{token}', { params: { path: { token } } })),
    refetchInterval: (query) => (query.state.data && finished.has(query.state.data.status) ? false : 15_000),
  });
}
