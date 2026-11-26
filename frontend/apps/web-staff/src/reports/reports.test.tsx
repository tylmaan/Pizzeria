import type { ReportDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const report: ReportDto = {
  locationId: null,
  from: '2026-11-20',
  to: '2026-11-26',
  summary: { completedOrders: 3, cancelledOrders: 1, revenue: 175.5, averageOrderValue: 58.5 },
  revenue: [
    { day: '2026-11-25', paymentMethod: 'Cash', orders: 1, total: 74, deliveryFees: 6 },
    { day: '2026-11-25', paymentMethod: 'Online', orders: 1, total: 74.5, deliveryFees: 6 },
    { day: '2026-11-26', paymentMethod: 'CardOnDelivery', orders: 1, total: 27, deliveryFees: 0 },
  ],
  bestSellers: [{ productName: 'Margherita', quantity: 4, revenue: 120 }],
  times: { toAccept: 1.5, acceptToReady: 14.25, delivery: 21, placedToCompleted: null },
  drivers: [{ driverId: 'd-1', driverName: 'Marek', deliveries: 2, averageDeliveryMinutes: 20.5, total: 148.5 }],
};

const locations = [{ id: 'l-1', name: 'Centrum', address: { street: 'x', postalCode: '00-000', city: 'y' }, timeZoneId: 'Europe/Warsaw', isActive: true, position: null }];

describe('reports', () => {
  test('company admin sees the whole company by default with revenue per day and payment method', async () => {
    const { requests } = renderApp({
      path: '/reports',
      signedIn: true,
      routes: { 'GET /api/auth/me': json(user()), 'GET /api/locations': json(locations), 'GET /api/reports': json(report) },
    });

    expect(await screen.findByText(/175,50\s*zł/)).toBeTruthy();
    const day25 = screen.getByRole('row', { name: /2026-11-25/ });
    expect(within(day25).getAllByText(/74,00\s*zł|74,50\s*zł|148,50\s*zł/)).toHaveLength(3);
    expect(screen.getByRole('row', { name: /Marek/ })).toBeTruthy();
    expect(screen.getByText('14,3')).toBeTruthy();
    expect(screen.getByText('—')).toBeTruthy(); // no data for one of the times

    const call = requests.find((r) => new URL(r.url).pathname === '/api/reports')!;
    expect(new URL(call.url).searchParams.has('locationId')).toBe(false);
  });

  test('a location manager reports on own locations only and can change the range', async () => {
    const { requests } = renderApp({
      path: '/reports',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['LocationManager'], locationIds: ['l-1'] })),
        'GET /api/locations': json(locations),
        'GET /api/reports': json(report),
      },
    });

    await screen.findByText(/175,50\s*zł/);
    expect(screen.queryByRole('option', { name: 'Cała firma' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Dziś' }));

    await waitFor(() => {
      const calls = requests.filter((r) => new URL(r.url).pathname === '/api/reports').map((r) => new URL(r.url).searchParams);
      const last = calls.at(-1)!;
      expect(last.get('locationId')).toBe('l-1');
      expect(last.get('from')).toBe(last.get('to'));
    });
  });
});
