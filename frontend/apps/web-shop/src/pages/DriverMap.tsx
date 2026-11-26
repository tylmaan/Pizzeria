import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';

/** Small map with the driver's position (loaded lazily: Leaflet is only needed while the order is on its way). */
export function DriverMap({ latitude, longitude, label }: { latitude: number; longitude: number; label: string }) {
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const marker = useRef<L.CircleMarker | null>(null);

  useEffect(() => {
    if (!container.current) {
      return undefined;
    }
    const instance = L.map(container.current, { zoomControl: false }).setView([latitude, longitude], 15);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(instance);
    marker.current = L.circleMarker([latitude, longitude], { radius: 10, color: '#1d4ed8', fillOpacity: 0.9, weight: 3 }).addTo(instance);
    map.current = instance;
    return () => {
      instance.remove();
      map.current = null;
    };
    // The map is created once; position changes only move the marker below.
  }, []);

  useEffect(() => {
    marker.current?.setLatLng([latitude, longitude]);
    map.current?.panTo([latitude, longitude]);
  }, [latitude, longitude]);

  return <div ref={container} role="img" aria-label={label} className="h-64 w-full rounded-xl" />;
}

export default DriverMap;
