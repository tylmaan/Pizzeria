import type { PrintAgentDto, PrintJobDto } from '@pizzeria/api-client';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const locations = [{ id: 'l-1', name: 'Centrum', address: { street: 'x', postalCode: '00-000', city: 'y' }, timeZoneId: 'Europe/Warsaw', isActive: true, position: null }];

const agent = (overrides: Partial<PrintAgentDto> = {}): PrintAgentDto => ({
  id: 'a-1',
  locationId: 'l-1',
  name: 'Kuchnia PC',
  isOnline: true,
  lastSeenAt: '2026-11-26T17:00:00Z',
  receiptPrinterOnline: true,
  fiscalPrinterOnline: null,
  isRevoked: false,
  ...overrides,
});

const job = (overrides: Partial<PrintJobDto> = {}): PrintJobDto => ({
  id: 'j-1',
  orderId: 'o-1',
  kind: 'KitchenTicket',
  status: 'Printed',
  attempts: 1,
  error: null,
  createdAt: '2026-11-26T17:00:00Z',
  printedAt: '2026-11-26T17:00:05Z',
  ...overrides,
});

const settings = { printKitchenTickets: true, fiscalReceiptTrigger: 'Never' };

describe('printing', () => {
  test('manager sees agents with printer status, failed jobs can be retried', async () => {
    const { requests } = renderApp({
      path: '/printing',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['LocationManager'], locationIds: ['l-1'] })),
        'GET /api/locations': json(locations),
        'GET /api/locations/l-1/print-agents': json([agent(), agent({ id: 'a-2', name: 'Stary laptop', isRevoked: true })]),
        'GET /api/locations/l-1/print-settings': json(settings),
        'GET /api/locations/l-1/print-jobs': json([job(), job({ id: 'j-2', status: 'Failed', error: 'Brak papieru' })]),
        'POST /api/print-jobs/j-2/retry': json(job({ id: 'j-2', status: 'Pending' })),
      },
    });

    const pc = await screen.findByRole('listitem', { name: 'Kuchnia PC' });
    expect(within(pc).getByText(/Drukarka bonów: online · Drukarka fiskalna: brak/)).toBeTruthy();
    expect(screen.queryByText('Stary laptop')).toBeNull(); // revoked agents are hidden
    expect(await screen.findByText(/Błąd — Brak papieru/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Ponów' }));

    await waitFor(() => expect(requests.some((r) => r.method === 'POST' && r.url.endsWith('/api/print-jobs/j-2/retry'))).toBe(true));
  });

  test('registering a computer shows its token once', async () => {
    renderApp({
      path: '/printing',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user()),
        'GET /api/locations': json(locations),
        'GET /api/locations/l-1/print-agents': json([]),
        'GET /api/locations/l-1/print-settings': json(settings),
        'GET /api/locations/l-1/print-jobs': json([]),
        'POST /api/locations/l-1/print-agents': json({ agent: agent({ isOnline: false, lastSeenAt: null }), token: 'secret-device-token' }, 201),
      },
    });

    expect(await screen.findByText(/Brak podłączonych komputerów/)).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Nazwa komputera'), { target: { value: 'Kuchnia PC' } });
    fireEvent.click(screen.getByRole('button', { name: 'Dodaj' }));

    expect(await screen.findByText('secret-device-token')).toBeTruthy();
  });

  test('print settings are saved for the location', async () => {
    const { requests } = renderApp({
      path: '/printing',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user()),
        'GET /api/locations': json(locations),
        'GET /api/locations/l-1/print-agents': json([agent()]),
        'GET /api/locations/l-1/print-settings': json(settings),
        'GET /api/locations/l-1/print-jobs': json([]),
        'PUT /api/locations/l-1/print-settings': json({ printKitchenTickets: false, fiscalReceiptTrigger: 'OnCompleted' }),
      },
    });

    fireEvent.click(await screen.findByLabelText('Drukuj bon kuchenny po przyjęciu zamówienia'));
    fireEvent.change(screen.getByLabelText('Paragon fiskalny'), { target: { value: 'OnCompleted' } });
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Zapisano.')).toBeTruthy();
    const put = requests.find((r) => r.method === 'PUT')!;
    expect(await put.json()).toEqual({ printKitchenTickets: false, fiscalReceiptTrigger: 'OnCompleted' });
  });

  test('staff see status and jobs but cannot manage computers or settings', async () => {
    renderApp({
      path: '/printing',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user({ roles: ['Staff'], locationIds: ['l-1'] })),
        'GET /api/locations': json(locations),
        'GET /api/locations/l-1/print-agents': json([agent({ isOnline: false })]),
        'GET /api/locations/l-1/print-jobs': json([job({ status: 'Pending', printedAt: null })]),
      },
    });

    const pc = await screen.findByRole('listitem', { name: 'Kuchnia PC' });
    expect(within(pc).getByText('offline')).toBeTruthy();
    expect(await screen.findByText('Czeka na drukarkę')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Odłącz' })).toBeNull();
    expect(screen.queryByText('Ustawienia druku')).toBeNull();
  });
});
