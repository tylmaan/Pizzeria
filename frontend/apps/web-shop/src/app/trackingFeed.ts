import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

/** Notifies when the tracked order changes (or the connection came back); returns an unsubscribe function. */
export type TrackingFeed = (token: string, onChange: () => void, onConnected: (connected: boolean) => void) => () => void;

/** Anonymous SignalR subscription; the shop host identifies the company, the token the order. */
export const signalRTrackingFeed: TrackingFeed = (token, onChange, onConnected) => {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/orders')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('OrderChanged', () => onChange());
  connection.onreconnecting(() => onConnected(false));
  connection.onreconnected(async () => {
    await connection.invoke('TrackOrder', token);
    onConnected(true);
    onChange();
  });
  connection.onclose(() => onConnected(false));

  void (async () => {
    try {
      await connection.start();
      await connection.invoke('TrackOrder', token);
      onConnected(true);
    } catch {
      onConnected(false);
    }
  })();

  return () => void connection.stop();
};
