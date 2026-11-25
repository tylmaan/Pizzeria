import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const location = {
  id: 'l-1',
  name: 'Demo Pizza Centrum',
  address: { street: 'ul. Lipowa 1', postalCode: '15-001', city: 'Białystok' },
  timeZoneId: 'Europe/Warsaw',
  isActive: true,
};

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });

describe('locations', () => {
  test('shows field errors returned by the API next to the fields', async () => {
    renderApp({
      path: '/company/locations/new',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user()),
        'POST /api/locations': json(
          {
            status: 400,
            code: 'common.validation_failed',
            errors: { name: ['validation.not_empty'], 'address.postalCode': ['validation.postal_code'] },
          },
          400,
        ),
      },
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Sprawdź zaznaczone pola.')).toBeTruthy();
    expect(screen.getByText('To pole jest wymagane.')).toBeTruthy();
    expect(screen.getByText('Kod pocztowy w formacie 00-000.')).toBeTruthy();
  });

  test('editing sends the whole location including the active flag', async () => {
    const { bodiesOf, router } = renderApp({
      path: '/company/locations/l-1',
      signedIn: true,
      routes: {
        'GET /api/auth/me': json(user()),
        'GET /api/locations/l-1': json(location),
        'PUT /api/locations/l-1': json({ ...location, name: 'Rynek', isActive: false }),
        'GET /api/locations': json([]),
      },
    });

    expect(await screen.findByDisplayValue('Demo Pizza Centrum')).toBeTruthy();
    type('Nazwa lokalu', 'Rynek');
    fireEvent.click(screen.getByLabelText('Lokal aktywny'));
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    await screen.findByRole('heading', { name: 'Lokale' });
    expect(router.state.location.pathname).toBe('/company/locations');
    expect(await bodiesOf('PUT /api/locations/l-1')).toEqual([
      { name: 'Rynek', address: location.address, timeZoneId: 'Europe/Warsaw', isActive: false },
    ]);
  });
});

describe('staff', () => {
  const staffRoutes = {
    'GET /api/auth/me': json(user()),
    'GET /api/locations': json([location]),
    'GET /api/users/assignable-roles': json([
      { role: 'CompanyAdmin', requiresLocations: false },
      { role: 'Staff', requiresLocations: true },
    ]),
    'POST /api/users': json({ id: 'u-2' }, 201),
    'GET /api/users': json([]),
  };

  test('location picker appears only for location-scoped roles and selected locations are sent', async () => {
    const { bodiesOf } = renderApp({ path: '/company/staff/new', signedIn: true, routes: staffRoutes });

    const role = await screen.findByLabelText('Rola');
    await screen.findByRole('option', { name: 'Obsługa' });
    fireEvent.change(role, { target: { value: 'CompanyAdmin' } });
    expect(screen.queryByLabelText('Demo Pizza Centrum')).toBeNull();

    fireEvent.change(role, { target: { value: 'Staff' } });
    fireEvent.click(screen.getByLabelText('Demo Pizza Centrum'));
    type('Imię i nazwisko', 'Anna Nowak');
    type('E-mail (login)', 'anna@demo.local');
    type('Hasło startowe', 'haslo1234');
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Brak pracowników.')).toBeTruthy();
    expect(await bodiesOf('POST /api/users')).toEqual([
      {
        displayName: 'Anna Nowak',
        email: 'anna@demo.local',
        password: 'haslo1234',
        role: 'Staff',
        locationIds: ['l-1'],
      },
    ]);
  });

  test('business errors from the API are shown as a banner', async () => {
    renderApp({
      path: '/company/staff/new',
      signedIn: true,
      routes: { ...staffRoutes, 'POST /api/users': json({ status: 409, code: 'user.email_taken' }, 409) },
    });

    const role = await screen.findByLabelText('Rola');
    await screen.findByRole('option', { name: 'Administrator firmy' });
    fireEvent.change(role, { target: { value: 'CompanyAdmin' } });
    fireEvent.click(screen.getByRole('button', { name: 'Zapisz' }));

    expect(await screen.findByText('Ten e-mail jest już używany.')).toBeTruthy();
  });

  test('staff list shows translated roles and location names', async () => {
    renderApp({
      path: '/company/staff',
      signedIn: true,
      routes: {
        ...staffRoutes,
        'GET /api/users': json([
          { id: 'u-3', email: 'k@demo.local', displayName: 'Kasia', role: 'Kitchen', locationIds: ['l-1'] },
        ]),
      },
    });

    expect(await screen.findByText('Kasia')).toBeTruthy();
    expect(screen.getByText('Kuchnia')).toBeTruthy();
    expect(await screen.findByText('Demo Pizza Centrum')).toBeTruthy();
  });
});
