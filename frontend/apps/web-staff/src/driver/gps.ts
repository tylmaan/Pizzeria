import { useEffect, useRef, useState } from 'react';
import { useServices } from '../app/services';

export type GpsState = 'off' | 'waiting' | 'ok' | 'stale' | 'denied' | 'unavailable';

type Fix = { latitude: number; longitude: number; accuracy: number; at: number };

/**
 * Sends the driver's position while the shift is open: watches GPS and posts the newest fix at most every
 * `intervalMs`. A PWA only gets positions while it is open (especially on iOS), so the state turns
 * `stale` when no fix arrived for `staleAfterMs` and the app asks the driver to keep it in front.
 */
export function useGpsReporter(
  active: boolean,
  {
    intervalMs = 10_000,
    staleAfterMs = 60_000,
    geolocation = typeof navigator === 'undefined' ? undefined : navigator.geolocation,
  }: { intervalMs?: number; staleAfterMs?: number; geolocation?: Geolocation | undefined } = {},
): GpsState {
  const { api } = useServices();
  const [state, setState] = useState<GpsState>('off');
  const latest = useRef<Fix | null>(null);
  const lastSentAt = useRef(0);

  useEffect(() => {
    if (!active) {
      setState('off');
      return undefined;
    }
    if (!geolocation) {
      setState('unavailable');
      return undefined;
    }

    setState('waiting');
    const watchId = geolocation.watchPosition(
      (position) => {
        latest.current = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy,
          at: Date.now(),
        };
        setState('ok');
      },
      (error) => setState(error.code === error.PERMISSION_DENIED ? 'denied' : 'waiting'),
      { enableHighAccuracy: true, maximumAge: 5_000 },
    );

    const timer = setInterval(() => {
      const fix = latest.current;
      if (!fix) {
        return;
      }
      if (Date.now() - fix.at > staleAfterMs) {
        setState('stale');
        return;
      }
      if (fix.at > lastSentAt.current) {
        lastSentAt.current = fix.at;
        void api
          .POST('/api/driver/position', { body: { latitude: fix.latitude, longitude: fix.longitude, accuracyMeters: fix.accuracy } })
          .catch(() => {
            // Offline for a moment: the next fix is sent on the next tick.
          });
      }
    }, intervalMs);

    return () => {
      geolocation.clearWatch(watchId);
      clearInterval(timer);
    };
  }, [active, api, geolocation, intervalMs, staleAfterMs]);

  return state;
}
