import createClient, { type Client, type Middleware } from 'openapi-fetch';
import type { AuthSession } from './auth';
import { ApiError, type ProblemDetails } from './errors';
import type { paths } from './schema';

export type ApiClient = Client<paths>;

export interface ApiClientOptions {
  session: AuthSession;
  /** Origin of the API. Defaults to the page origin (the dev server proxies `/api`). */
  baseUrl?: string;
  fetch?: (request: Request) => Promise<Response>;
}

export function createApiClient({ session, baseUrl, fetch }: ApiClientOptions): ApiClient {
  const client = createClient<paths>({
    baseUrl: baseUrl ?? globalThis.location?.origin ?? '',
    ...(fetch ? { fetch } : {}),
  });
  client.use(authMiddleware(session));
  return client;
}

function authMiddleware(session: AuthSession): Middleware {
  return {
    async onRequest({ request }) {
      const token = await session.getAccessToken();
      if (token !== null) {
        request.headers.set('Authorization', `Bearer ${token}`);
      }
      return request;
    },
    onResponse({ response }) {
      // Access token rejected: drop the session so the app falls back to the login screen.
      if (response.status === 401) {
        session.logout();
      }
      return response;
    },
  };
}

type FetchResult<T> =
  | { data: T; error?: never; response: Response }
  | { data?: never; error: unknown; response: Response };

/** Returns the response body or throws {@link ApiError} — the shape TanStack Query expects. */
export async function unwrap<T>(request: Promise<FetchResult<T>>): Promise<T> {
  const result = await request;
  if (!result.response.ok || result.error !== undefined) {
    throw new ApiError(result.response.status, result.error as ProblemDetails | undefined);
  }
  return result.data as T;
}
