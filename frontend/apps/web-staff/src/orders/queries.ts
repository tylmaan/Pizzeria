import { unwrap, type OrderListScope } from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useServices } from '../app/services';
import type { FeedState } from '../app/ordersFeed';
import { useCurrentUser } from '../auth/session';

export const ordersKey = (locationId: string) => ['orders', locationId] as const;

export function useOrders(locationId: string, scope: OrderListScope = 'Active', live: FeedState = 'connected') {
  const { api } = useServices();
  return useQuery({
    queryKey: [...ordersKey(locationId), scope],
    queryFn: () =>
      unwrap(api.GET('/api/locations/{locationId}/orders', { params: { path: { locationId }, query: { scope } } })),
    enabled: locationId !== '',
    // Without the live feed fall back to polling, so the screen never silently goes stale.
    refetchInterval: live === 'connected' ? false : 30_000,
  });
}

/** Keeps the location's order lists fresh via SignalR and alerts on new orders. */
export function useOrdersLive(locationId: string, { alertOnNew }: { alertOnNew: boolean }): FeedState {
  const { ordersFeed, queryClient, newOrderAlert } = useServices();
  const [state, setState] = useState<FeedState>('disconnected');

  useEffect(() => {
    if (locationId === '') {
      return undefined;
    }
    return ordersFeed(
      locationId,
      (change) => {
        if (alertOnNew && change?.status === 'New') {
          newOrderAlert();
        }
        void queryClient.invalidateQueries({ queryKey: ordersKey(locationId) });
      },
      setState,
    );
  }, [locationId, alertOnNew, ordersFeed, queryClient, newOrderAlert]);

  return state;
}

function useOrderMutation<TVariables>(locationId: string, mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ordersKey(locationId) }),
  });
}

export function useAdvanceOrder(locationId: string) {
  const { api } = useServices();
  return useOrderMutation(locationId, (id: string) =>
    unwrap(api.POST('/api/orders/{id}/advance', { params: { path: { id } } })),
  );
}

export function useCancelOrder(locationId: string) {
  const { api } = useServices();
  return useOrderMutation(locationId, ({ id, reason }: { id: string; reason: string }) =>
    unwrap(api.POST('/api/orders/{id}/cancel', { params: { path: { id } }, body: { reason } })),
  );
}

export function useAssignDriver(locationId: string) {
  const { api } = useServices();
  return useOrderMutation(locationId, ({ id, driverId }: { id: string; driverId: string }) =>
    unwrap(api.POST('/api/orders/{id}/driver', { params: { path: { id } }, body: { driverId } })),
  );
}

export function useDrivers(locationId: string, enabled: boolean) {
  const { api } = useServices();
  return useQuery({
    queryKey: ['drivers', locationId],
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/drivers', { params: { path: { locationId } } })),
    enabled: enabled && locationId !== '',
    staleTime: 60_000,
  });
}

const storageKey = 'pizzeria.location';

/**
 * The location the screen works for: the user's only location, or the one picked last time
 * (managers with several locations). Remembered per browser.
 */
export function useCurrentLocationId(): [string, (id: string) => void] {
  const user = useCurrentUser();
  const [picked, setPicked] = useState(() => {
    try {
      return localStorage.getItem(storageKey);
    } catch {
      return null;
    }
  });
  const current = picked && user.locationIds.includes(picked) ? picked : (user.locationIds[0] ?? '');
  const pick = (id: string) => {
    setPicked(id);
    try {
      localStorage.setItem(storageKey, id);
    } catch {
      // Not remembered: fine.
    }
  };
  return [current, pick];
}

/** Current time, ticking every 30 s — for "12 min ago" on order cards. */
export function useNow(intervalMs = 30_000): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);
  return now;
}
