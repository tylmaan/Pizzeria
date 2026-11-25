import type { DeliveryZoneDto, LocationDto } from '@pizzeria/api-client';
import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, test, vi } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';
import type { ZoneMapProps } from './ZoneMap';

// Leaflet needs a real browser; the stand-in lets tests "click the map" through a button.
const clicks = [
  { latitude: 53.12, longitude: 23.15 },
  { latitude: 53.12, longitude: 23.18 },
  { latitude: 53.14, longitude: 23.18 },
];
vi.mock('./ZoneMap', () => ({
  ZoneMap: ({ label, boundary, onBoundaryChange, marker }: ZoneMapProps) => (
    <div role="region" aria-label={label}>
      {marker && <span>marker {marker.latitude}</span>}
      {onBoundaryChange && boundary && (
        <button type="button" onClick={() => onBoundaryChange([...boundary, clicks[boundary.length % clicks.length]!])}>
          map click
        </button>
      )}
    </div>
  ),
}));

const location: LocationDto = {
  id: 'l-1',
  name: 'Centrum',
  address: { street: 'ul. Lipowa 1', postalCode: '15-001', city: 'Białystok' },
  timeZoneId: 'Europe/Warsaw',
  isActive: true,
  position: { latitude: 53.13, longitude: 23.16 },
};

const zone: DeliveryZoneDto = {
  id: 'z-1',
  locationId: 'l-1',
  name: 'Śródmieście',
  boundary: clicks,
  deliveryFee: 6.5,
  minOrderValue: 40,
  estimatedMinutes: 35,
  isActive: true,
};

const baseRoutes = {
  'GET /api/auth/me': json(user()),
  'GET /api/locations/l-1': json(location),
  'GET /api/locations/l-1/delivery-zones': json([zone]),
};

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('delivery zones', () => {
  test('lists zones with their terms and checks an address', async () => {
    const { bodiesOf } = renderApp({
      path: '/company/locations/l-1/zones',
      signedIn: true,
      routes: {
        ...baseRoutes,
        'POST /api/delivery/check': json({
          delivers: true,
          point: { latitude: 53.13, longitude: 23.16 },
          formattedAddress: 'ul. Rynek 1, 15-001 Białystok',
          locationId: 'l-1',
          locationName: 'Centrum',
          zoneId: 'z-1',
          zoneName: 'Śródmieście',
          deliveryFee: 6.5,
          minOrderValue: 40,
          estimatedMinutes: 35,
        }),
      },
    });

    expect(await screen.findByText(/dostawa 6,50\s*zł · min\. 40,00\s*zł · ok\. 35 min/)).toBeTruthy();
    type('Ulica i numer', 'ul. Rynek 1');
    fireEvent.click(screen.getByRole('button', { name: 'Sprawdź' }));

    expect((await screen.findByRole('status')).textContent).toMatch(/Dowozimy: Centrum, strefa „Śródmieście”/);
    expect(screen.getByText('marker 53.13')).toBeTruthy();
    expect(await bodiesOf('POST /api/delivery/check')).toEqual([
      { street: 'ul. Rynek 1', postalCode: '15-001', city: 'Białystok' },
    ]);
  });

  test('new zone sends the drawn boundary and amounts typed with decimal commas', async () => {
    const { bodiesOf, router } = renderApp({
      path: '/company/locations/l-1/zones/new',
      signedIn: true,
      routes: { ...baseRoutes, 'POST /api/locations/l-1/delivery-zones': json(zone, 201) },
    });

    await screen.findByLabelText('Nazwa strefy');
    type('Nazwa strefy', 'Śródmieście');
    type('Cena dostawy (zł)', '6,50');
    type('Minimalne zamówienie (zł)', '40');
    type('Szacowany czas dostawy (min)', '35');
    for (let i = 0; i < 4; i++) {
      fireEvent.click(screen.getByRole('button', { name: 'map click' }));
    }
    fireEvent.click(screen.getByRole('button', { name: 'Cofnij punkt' }));
    expect(screen.getByText('Punkty granicy: 3')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    await screen.findByText(/dostawa 6,50/);
    expect(router.state.location.pathname).toBe('/company/locations/l-1/zones');
    expect(await bodiesOf('POST /api/locations/l-1/delivery-zones')).toEqual([
      { name: 'Śródmieście', boundary: clicks, deliveryFee: 6.5, minOrderValue: 40, estimatedMinutes: 35, isActive: true },
    ]);
  });

  test('a zone without a drawn boundary is not sent', async () => {
    const { bodiesOf } = renderApp({ path: '/company/locations/l-1/zones/new', signedIn: true, routes: baseRoutes });

    await screen.findByLabelText('Nazwa strefy');
    type('Nazwa strefy', 'Pusta');
    type('Cena dostawy (zł)', '5');
    type('Minimalne zamówienie (zł)', '30');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Wyznacz na mapie co najmniej 3 punkty.')).toBeTruthy();
    expect(await bodiesOf('POST /api/locations/l-1/delivery-zones')).toEqual([]);
  });

  test('invalid boundary reported by the API is shown', async () => {
    renderApp({
      path: '/company/locations/l-1/zones/z-1',
      signedIn: true,
      routes: {
        ...baseRoutes,
        'PUT /api/locations/l-1/delivery-zones/z-1': json({ status: 422, code: 'delivery.invalid_boundary' }, 422),
      },
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Nieprawidłowa granica strefy (np. linie się przecinają).')).toBeTruthy();
  });
});
