import { unwrap, type MeResponse, type Role } from '@pizzeria/api-client';
import { useQuery } from '@tanstack/react-query';
import { createContext, useContext, useSyncExternalStore } from 'react';
import { useServices } from '../app/services';

export function useSignedIn(): boolean {
  const { session } = useServices();
  return useSyncExternalStore(session.subscribe, () => session.isSignedIn);
}

export function useMeQuery() {
  const { api } = useServices();
  return useQuery({
    queryKey: ['me'],
    queryFn: () => unwrap(api.GET('/api/auth/me')),
    enabled: useSignedIn(),
    staleTime: Infinity,
  });
}

export const CurrentUserContext = createContext<MeResponse | null>(null);

/** The signed-in user. Only valid below <RequireAuth>. */
export function useCurrentUser(): MeResponse {
  const user = useContext(CurrentUserContext);
  if (user === null) {
    throw new Error('useCurrentUser must be used below <RequireAuth>.');
  }
  return user;
}

export function hasAnyRole(user: MeResponse, roles: readonly Role[]): boolean {
  return user.roles.some((role) => (roles as readonly string[]).includes(role));
}
