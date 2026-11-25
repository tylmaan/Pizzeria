import { describe, expect, test, vi } from 'vitest';
import { AuthSession, type RefreshTokenStore } from './auth';
import { ApiError } from './errors';

function memoryStore(initial: string | null = null): RefreshTokenStore & { value: string | null } {
  return {
    value: initial,
    load() {
      return this.value;
    },
    save(token) {
      this.value = token;
    },
  };
}

function tokens(access: string, refresh: string, expiresIn = 1800) {
  return Response.json({ tokenType: 'Bearer', accessToken: access, refreshToken: refresh, expiresIn });
}

describe('AuthSession', () => {
  test('login keeps the access token in memory and persists only the refresh token', async () => {
    const store = memoryStore();
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(tokens('access-1', 'refresh-1'));
    const session = new AuthSession({ store, fetch });

    await session.login('owner@demo.local', 'secret-1');

    expect(session.isSignedIn).toBe(true);
    expect(store.value).toBe('refresh-1');
    expect(await session.getAccessToken()).toBe('access-1');
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  test('failed login throws ApiError with the server error code', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      Response.json({ status: 401, code: 'auth.invalid_credentials' }, { status: 401 }),
    );
    const session = new AuthSession({ store: memoryStore(), fetch });

    const error = await session.login('owner@demo.local', 'wrong').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('auth.invalid_credentials');
    expect(session.isSignedIn).toBe(false);
  });

  test('refreshes shortly before expiry and shares one refresh between concurrent callers', async () => {
    let now = 0;
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(tokens('access-1', 'refresh-1', 60))
      .mockResolvedValueOnce(tokens('access-2', 'refresh-2', 60));
    const session = new AuthSession({ store: memoryStore(), fetch, now: () => now });
    await session.login('owner@demo.local', 'secret-1');

    now = 45_000; // inside the 30 s safety margin before the 60 s expiry
    const [a, b] = await Promise.all([session.getAccessToken(), session.getAccessToken()]);

    expect([a, b]).toEqual(['access-2', 'access-2']);
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[1]?.[0]).toBe('/api/auth/refresh');
  });

  test('restores the session from the stored refresh token after a reload', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(tokens('access-2', 'refresh-2'));
    const session = new AuthSession({ store: memoryStore('refresh-1'), fetch });

    expect(session.isSignedIn).toBe(true);
    expect(await session.getAccessToken()).toBe('access-2');
    expect(JSON.parse(fetch.mock.calls[0]?.[1]?.body as string)).toEqual({ refreshToken: 'refresh-1' });
  });

  test('rejected refresh token signs the user out and notifies subscribers', async () => {
    const store = memoryStore('stale');
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(new Response(null, { status: 401 }));
    const session = new AuthSession({ store, fetch });
    const listener = vi.fn();
    session.subscribe(listener);

    expect(await session.getAccessToken()).toBeNull();

    expect(session.isSignedIn).toBe(false);
    expect(store.value).toBeNull();
    expect(listener).toHaveBeenCalledTimes(1);
  });

  test('network failure during refresh keeps the user signed in', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch'));
    const session = new AuthSession({ store: memoryStore('refresh-1'), fetch });

    await expect(session.getAccessToken()).rejects.toThrow(TypeError);

    expect(session.isSignedIn).toBe(true);
  });
});
