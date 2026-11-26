import { unwrap, type GeoPointDto, type StaffOrderDto } from '@pizzeria/api-client';
import { useTranslation } from '@pizzeria/i18n';
import { useQuery } from '@tanstack/react-query';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import { useServices } from '../app/services';

function useDriverPositions(locationId: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: ['driver-positions', locationId],
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/drivers/positions', { params: { path: { locationId } } })),
    // Drivers report every ~10 s; this map is a glance, so polling is enough.
    refetchInterval: 15_000,
  });
}

/** Drivers on shift (blue) and destinations of active deliveries (orange) of the location. */
export function LiveMap({ locationId, orders, center }: { locationId: string; orders: StaffOrderDto[]; center: GeoPointDto | null }) {
  const { t } = useTranslation();
  const positions = useDriverPositions(locationId);
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const layers = useRef<L.LayerGroup | null>(null);

  useEffect(() => {
    if (!container.current) {
      return undefined;
    }
    const instance = L.map(container.current).setView(center ? [center.latitude, center.longitude] : [52.07, 19.48], center ? 13 : 6);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(instance);
    layers.current = L.layerGroup().addTo(instance);
    map.current = instance;
    return () => {
      instance.remove();
      map.current = null;
    };
  }, [center]);

  useEffect(() => {
    const group = layers.current;
    if (!group) {
      return;
    }
    group.clearLayers();
    for (const order of orders) {
      if (order.deliveryPoint && (order.status === 'AssignedToDriver' || order.status === 'InDelivery' || order.status === 'Ready')) {
        L.circleMarker([order.deliveryPoint.latitude, order.deliveryPoint.longitude], { radius: 7, color: '#ea580c', fillOpacity: 0.9 })
          .bindTooltip(`#${order.number}`, { permanent: true, direction: 'top' })
          .addTo(group);
      }
    }
    for (const driver of positions.data ?? []) {
      const seconds = Math.round((Date.now() - Date.parse(driver.recordedAt)) / 1000);
      L.circleMarker([driver.latitude, driver.longitude], { radius: 10, color: '#1d4ed8', fillOpacity: 0.9, weight: 3 })
        .bindTooltip(t('liveMap.driverAge', { name: driver.driverName ?? '?', seconds }), { permanent: true, direction: 'right' })
        .addTo(group);
    }
  }, [orders, positions.data, t]);

  return (
    <section className="flex flex-col gap-2">
      <div ref={container} role="region" aria-label={t('liveMap.title')} className="h-96 w-full rounded-xl border-2 border-neutral-300" />
      {positions.data?.length === 0 && <p className="text-base text-neutral-600">{t('liveMap.none')}</p>}
    </section>
  );
}
