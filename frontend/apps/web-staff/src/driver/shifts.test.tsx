import type { DriverShiftDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

function shift(overrides: Partial<DriverShiftDto> = {}): DriverShiftDto {
  return {
    id: 's-1', locationId: 'l-1', driverId: 'd-1', driverName: 'Marek', status: 'Open',
    startedAt: '2026-11-26T15:00:00Z', endedAt: null, startingCash: 100, deliveriesCount: 0,
    cashCollected: 0, cardCollected: 0, paidOnline: 0, tips: 0, expectedCash: 100,
    countedCash: null, discrepancy: null, discrepancyComment: null, settledAt: null,
    ...overrides,
  };
}

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('driver shift', () => {
  test('driver starts a shift with change money and ends it with a summary of cash to hand over', async () => {
    let current: DriverShiftDto | null = null;
    const closed = shift({ status: 'Closed', endedAt: '2026-11-26T21:00:00Z', deliveriesCount: 3, cashCollected: 126.5, cardCollected: 61, tips: 12, expectedCash: 226.5 });
    const { bodiesOf } = renderApp({
      path: '/driver',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['Driver'], locationIds: ['l-1'] })),
        'GET /api/locations/l-1/orders': json([]),
        'GET /api/driver/shifts/current': () => Response.json({ shift: current }),
        'POST /api/driver/shifts': () => {
          current = shift();
          return Response.json(current, { status: 201 });
        },
        'POST /api/driver/shifts/current/close': () => {
          current = null;
          return Response.json(closed);
        },
      },
    });

    await screen.findByText('Zmiana nie jest rozpoczęta.');
    type('Pogotówka (zł)', '100');
    fireEvent.click(screen.getByRole('button', { name: 'Rozpocznij zmianę' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Zakończ zmianę' }));
    type('Napiwki (zł)', '12');
    fireEvent.click(screen.getByRole('button', { name: 'Zakończ i podsumuj' }));

    const summary = await screen.findByRole('region', { name: 'Podsumowanie zmiany' });
    expect(within(summary).getByText(/226,50\s*zł/)).toBeTruthy();
    expect(await bodiesOf('POST /api/driver/shifts')).toEqual([{ locationId: 'l-1', startingCash: 100 }]);
    expect(await bodiesOf('POST /api/driver/shifts/current/close')).toEqual([{ tips: 12 }]);
  });
});

describe('shift settlement', () => {
  test('manager counts the cash; a difference is shown and the comment is sent', async () => {
    const { bodiesOf } = renderApp({
      path: '/shifts',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['LocationManager'], locationIds: ['l-1'] })),
        'GET /api/locations': json([{ id: 'l-1', name: 'Centrum', address: { street: 'x', postalCode: '00-000', city: 'y' }, timeZoneId: 'Europe/Warsaw', isActive: true, position: null }]),
        'GET /api/locations/l-1/shifts': (request) =>
          Response.json(new URL(request.url).searchParams.get('status') === 'Closed'
            ? [shift({ status: 'Closed', endedAt: '2026-11-26T21:00:00Z', cashCollected: 74, expectedCash: 174 })]
            : []),
        'POST /api/shifts/s-1/settle': json(shift({ status: 'Settled', countedCash: 170, discrepancy: -4 })),
      },
    });

    const card = await screen.findByRole('article', { name: 'Marek' });
    expect((within(card).getByLabelText('Policzona gotówka (zł)') as HTMLInputElement).value).toBe('174,00');
    fireEvent.change(within(card).getByLabelText('Policzona gotówka (zł)'), { target: { value: '170' } });
    expect(within(card).getByText(/Różnica: -4,00\s*zł/)).toBeTruthy();
    fireEvent.change(within(card).getByLabelText('Komentarz (wymagany przy różnicy)'), { target: { value: 'Brak drobnych' } });
    fireEvent.click(within(card).getByRole('button', { name: 'Rozlicz' }));

    await waitFor(async () => expect(await bodiesOf('POST /api/shifts/s-1/settle')).toEqual([{ countedCash: 170, comment: 'Brak drobnych' }]));
  });
});
