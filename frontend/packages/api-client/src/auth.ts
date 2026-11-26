import { ApiError } from './errors';
import type { components } from './schema';

type AccessTokenResponse = components['schemas']['AccessTokenResponse'];

export interface RefreshTokenStore {
  load(): string | null;
  save(refreshToken: string | null): void;
}

/**
 * Keeps the refresh token in localStorage so staff stay signed in across reloads of the PWA.
 * The access token lives only in memory.
 */
export function localStorageTokenStore(key = 'pizzeria.refreshToken'): RefreshTokenStore {
  return {
    load: () => {
      try {
        return localStorage.getItem(key);
      } catch {
        return null;
      }
    },
    save: (refreshToken) => {
      try {
        if (refreshToken === null) {
          localStorage.removeItem(key);
        } else {
          localStorage.setItem(key, refreshToken);
        }
      } catch {
        // Storage unavailable (private mode): the session simply lasts until the tab is closed.
      }
    },
  };
}

export interface AuthSessionOptions {
  store: RefreshTokenStore;
  baseUrl?: string;
  fetch?: typeof fetch;
  now?: () => number;
}

/** Refresh this long before the access token expires, so requests in flight do not race the expiry. */
const REFRESH_MARGIN_MS = 30_000;

/** Bearer-token session against the Identity endpoints (`/api/auth/login`, `/api/auth/refresh`). */
export class AuthSession {
  readonly #store: RefreshTokenStore;
  readonly #baseUrl: string;
  readonly #fetch: typeof fetch;
  readonly #now: () => number;
  readonly #listeners = new Set<() => void>();

  #accessToken: string | null = null;
  #expiresAt = 0;
  #refreshToken: string | null;
  #pendingRefresh: Promise<string | null> | null = null;

  constructor(options: AuthSessionOptions) {
    this.#store = options.store;
    this.#baseUrl = options.baseUrl ?? '';
    this.#fetch = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
    this.#now = options.now ?? Date.now;
    this.#refreshToken = this.#store.load();
  }

  get isSignedIn(): boolean {
    return this.#refreshToken !== null;
  }

  /** For `useSyncExternalStore`: notified when the user signs in or out. */
  subscribe = (listener: () => void): (() => void) => {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  };

  login(email: string, password: string): Promise<void> {
    return this.signIn('/api/auth/login', { email, password });
  }

  /** Calls an endpoint that answers with a token pair (staff login, shop login or registration) and keeps it. */
  async signIn(path: string, body: unknown): Promise<void> {
    const response = await this.#post(path, body);
    if (!response.ok) {
      throw await ApiError.fromResponse(response);
    }
    this.#accept((await response.json()) as AccessTokenResponse);
  }

  /** Drops the tokens locally. Bearer tokens are not revocable; the access token simply expires. */
  logout(): void {
    const wasSignedIn = this.isSignedIn;
    this.#accessToken = null;
    this.#expiresAt = 0;
    this.#refreshToken = null;
    this.#store.save(null);
    if (wasSignedIn) {
      this.#notify();
    }
  }

  /** A valid access token, refreshed when needed; `null` when signed out. Concurrent calls share one refresh. */
  getAccessToken(): Promise<string | null> {
    if (this.#accessToken !== null && this.#now() < this.#expiresAt - REFRESH_MARGIN_MS) {
      return Promise.resolve(this.#accessToken);
    }
    if (this.#refreshToken === null) {
      return Promise.resolve(null);
    }
    this.#pendingRefresh ??= this.#refresh().finally(() => {
      this.#pendingRefresh = null;
    });
    return this.#pendingRefresh;
  }

  async #refresh(): Promise<string | null> {
    // A network failure rejects here and keeps the refresh token: the next request retries.
    const response = await this.#post('/api/auth/refresh', { refreshToken: this.#refreshToken });
    if (response.status === 401) {
      // Refresh token expired or invalidated (password or role change): sign in again.
      this.logout();
      return null;
    }
    if (!response.ok) {
      throw await ApiError.fromResponse(response);
    }
    this.#accept((await response.json()) as AccessTokenResponse);
    return this.#accessToken;
  }

  #accept(tokens: AccessTokenResponse): void {
    const wasSignedIn = this.isSignedIn;
    this.#accessToken = tokens.accessToken;
    this.#expiresAt = this.#now() + Number(tokens.expiresIn) * 1000;
    this.#refreshToken = tokens.refreshToken;
    this.#store.save(tokens.refreshToken);
    if (!wasSignedIn) {
      this.#notify();
    }
  }

  #post(path: string, body: unknown): Promise<Response> {
    return this.#fetch(`${this.#baseUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
  }

  #notify(): void {
    for (const listener of this.#listeners) {
      listener();
    }
  }
}
