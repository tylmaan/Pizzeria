import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { AuthSession } from '@pizzeria/api-client';

export type OrderChange = { orderId: string; locationId: string; number: number; status: string };

export type FeedState = 'connected' | 'disconnected';

/**
 * Subscribes to live order changes of a location; returns an unsubscribe function.
 * The feed only signals "something changed" — screens reload the data from the REST API
 * (also after reconnecting, so nothing is lost while offline).
 */
export type OrdersFeed = (
  locationId: string,
  onChange: (change: OrderChange | null) => void,
  onState: (state: FeedState) => void,
) => () => void;

export function signalROrdersFeed(session: AuthSession): OrdersFeed {
  return (locationId, onChange, onState) => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders', { accessTokenFactory: async () => (await session.getAccessToken()) ?? '' })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(30_000, 1_000 * 2 ** ctx.previousRetryCount) })
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('OrderChanged', (change: OrderChange) => onChange(change));
    connection.onreconnecting(() => onState('disconnected'));
    connection.onreconnected(async () => {
      await connection.invoke('JoinLocation', locationId);
      onState('connected');
      onChange(null);
    });
    connection.onclose(() => onState('disconnected'));

    let stopped = false;
    const start = async () => {
      try {
        await connection.start();
        await connection.invoke('JoinLocation', locationId);
        onState('connected');
      } catch {
        onState('disconnected');
        if (!stopped && connection.state === HubConnectionState.Disconnected) {
          setTimeout(() => void start(), 10_000);
        }
      }
    };
    void start();

    return () => {
      stopped = true;
      void connection.stop();
    };
  };
}

/** Short beep for a new order (no audio file needed). Silently ignored where audio is unavailable. */
export function playNewOrderSound(): void {
  try {
    const audio = new AudioContext();
    const tone = audio.createOscillator();
    const gain = audio.createGain();
    tone.frequency.value = 880;
    gain.gain.setValueAtTime(0.3, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.6);
    tone.connect(gain).connect(audio.destination);
    tone.start();
    tone.stop(audio.currentTime + 0.6);
  } catch {
    // No audio (e.g. tests or blocked autoplay): the screen still updates.
  }
}
