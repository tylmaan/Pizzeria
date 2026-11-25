import {
  ApiError,
  AuthSession,
  createApiClient,
  localStorageTokenStore,
  type ApiClient,
  type RefreshTokenStore,
} from '@pizzeria/api-client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

export interface AppServices {
  session: AuthSession;
  api: ApiClient;
  queryClient: QueryClient;
}

export interface AppServicesOptions {
  /** Injected in tests; the browser fetch otherwise. */
  fetch?: (request: Request) => Promise<Response>;
  store?: RefreshTokenStore;
}

export function createAppServices({ fetch, store }: AppServicesOptions = {}): AppServices {
  const session = new AuthSession({
    store: store ?? localStorageTokenStore(),
    ...(fetch ? { fetch: (input, init) => fetch(new Request(toAbsoluteUrl(input), init)) } : {}),
  });

  return {
    session,
    api: createApiClient({ session, ...(fetch ? { fetch } : {}) }),
    queryClient: new QueryClient({
      defaultOptions: {
        queries: {
          // 4xx will not fix itself on retry; network errors and 5xx get two more attempts.
          retry: (failureCount, error) =>
            !(error instanceof ApiError && error.status < 500) && failureCount < 2,
        },
      },
    }),
  };
}

function toAbsoluteUrl(input: RequestInfo | URL): string {
  const url = input instanceof Request ? input.url : String(input);
  return new URL(url, globalThis.location.origin).toString();
}

const ServicesContext = createContext<AppServices | null>(null);

export function AppServicesProvider({ services, children }: { services: AppServices; children: ReactNode }) {
  // Signing out must not leave the previous user's data in the cache (shared computer in the restaurant).
  useEffect(
    () =>
      services.session.subscribe(() => {
        if (!services.session.isSignedIn) {
          services.queryClient.clear();
        }
      }),
    [services],
  );

  return (
    <ServicesContext.Provider value={services}>
      <QueryClientProvider client={services.queryClient}>{children}</QueryClientProvider>
    </ServicesContext.Provider>
  );
}

export function useServices(): AppServices {
  const services = useContext(ServicesContext);
  if (services === null) {
    throw new Error('useServices must be used inside <AppServicesProvider>.');
  }
  return services;
}
