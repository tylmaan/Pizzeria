import type { GeoPointDto } from '@pizzeria/api-client';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';

// Plain Leaflet (no React wrapper or drawing plugin): the only interaction needed is "click adds a point".
// OpenStreetMap tiles are fine for development; production needs a tile provider decision (CLAUDE.md).

const POLAND: L.LatLngTuple = [52.07, 19.48];
// The API types allow numbers as strings (JSON number handling), hence Number().
const toLatLng = (p: GeoPointDto): L.LatLngTuple => [Number(p.latitude), Number(p.longitude)];

export type ZoneMapProps = {
  /** Accessible name of the map region. */
  label: string;
  /** Zones shown for context (read-only). */
  zones: { id: string; name: string; boundary: GeoPointDto[]; isActive: boolean }[];
  /** Boundary being edited; omit for a read-only map. */
  boundary?: GeoPointDto[];
  onBoundaryChange?: (boundary: GeoPointDto[]) => void;
  /** Highlighted point, e.g. a checked address. */
  marker?: GeoPointDto | null;
  /** Where to look when there is nothing to fit the view to. */
  fallbackCenter?: GeoPointDto | null;
};

export function ZoneMap({ label, zones, boundary, onBoundaryChange, marker, fallbackCenter }: ZoneMapProps) {
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const layers = useRef<L.LayerGroup | null>(null);
  const fitted = useRef(false);
  const onChange = useRef(onBoundaryChange);
  const current = useRef(boundary);
  onChange.current = onBoundaryChange;
  current.current = boundary;

  useEffect(() => {
    if (!container.current) {
      return;
    }
    const instance = L.map(container.current).setView(POLAND, 6);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap',
    }).addTo(instance);
    layers.current = L.layerGroup().addTo(instance);
    instance.on('click', (event: L.LeafletMouseEvent) => {
      if (onChange.current && current.current) {
        onChange.current([...current.current, { latitude: event.latlng.lat, longitude: event.latlng.lng }]);
      }
    });
    map.current = instance;
    return () => {
      instance.remove();
      map.current = null;
      fitted.current = false;
    };
  }, []);

  useEffect(() => {
    const group = layers.current;
    const instance = map.current;
    if (!group || !instance) {
      return;
    }
    group.clearLayers();

    for (const zone of zones) {
      L.polygon(zone.boundary.map(toLatLng), {
        color: zone.isActive ? '#2563eb' : '#737373',
        weight: 2,
        fillOpacity: boundary ? 0.05 : 0.15,
      })
        .bindTooltip(zone.name)
        .addTo(group);
    }

    if (boundary && boundary.length > 0) {
      L.polygon(boundary.map(toLatLng), { color: '#ea580c', weight: 3, fillOpacity: 0.25 }).addTo(group);
      for (const point of boundary) {
        L.circleMarker(toLatLng(point), { radius: 6, color: '#ea580c', fillOpacity: 1 }).addTo(group);
      }
    }

    if (marker) {
      L.circleMarker(toLatLng(marker), { radius: 9, color: '#15803d', weight: 3, fillOpacity: 0.8 }).addTo(group);
    }

    // Fit once to what is there; afterwards keep the user's zoom while they draw.
    if (!fitted.current) {
      const points = [...zones.flatMap((z) => z.boundary), ...(boundary ?? [])].map(toLatLng);
      if (points.length > 0) {
        instance.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
        fitted.current = true;
      } else if (fallbackCenter) {
        instance.setView(toLatLng(fallbackCenter), 13);
        fitted.current = true;
      }
    }
    if (marker) {
      instance.panTo(toLatLng(marker));
    }
  }, [zones, boundary, marker, fallbackCenter]);

  return (
    <div
      ref={container}
      role="region"
      aria-label={label}
      className={`h-96 w-full rounded-lg border-2 border-neutral-300 ${onBoundaryChange ? 'cursor-crosshair' : ''}`}
    />
  );
}
