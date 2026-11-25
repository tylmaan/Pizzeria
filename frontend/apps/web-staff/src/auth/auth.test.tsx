import { fireEvent, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { json, renderApp, user } from '../test/renderApp';

const demoLocation = {
  id: 'l-1',
  name: 'Demo Pizza Centrum',
  address: { street: 'ul. Lipowa 1', postalCode: '15-001', city: 'Białystok' },
  timeZoneId: 'Europe/Warsaw',
  isActive: true,
};

function signIn(email = 'owner@demo.local', password = 'secret-1') {
  fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Hasło'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Zaloguj' }));
}

describe('signing in', () => {
  test('signed-out user opening a section is sent to the login screen', async () => {
    renderApp({ path: '/company/locations' });

    expect(await screen.findByRole('heading', { name: 'Zaloguj się' })).toBeTruthy();
  });

  test('wrong password shows the translated server error', async () => {
    renderApp({
      routes: { 'POST /api/auth/login': json({ status: 401, code: 'auth.invalid_credentials' }, 401) },
    });

    signIn('owner@demo.local', 'wrong');

    expect(await screen.findByText('Nieprawidłowy e-mail lub hasło.')).toBeTruthy();
  });

  test('company admin lands on the locations list with company navigation only', async () => {
    const { bodiesOf } = renderApp({
      routes: {
        'GET /api/auth/me': json(user()),
        'GET /api/locations': json([demoLocation]),
      },
    });

    signIn();

    expect(await screen.findByText('Demo Pizza Centrum')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Pracownicy' })).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Kuchnia' })).toBeNull();
    expect(await bodiesOf('POST /api/auth/login')).toEqual([{ email: 'owner@demo.local', password: 'secret-1' }]);
  });

  test('returns to the page the user originally wanted after signing in', async () => {
    const { router } = renderApp({
      path: '/company/staff',
      routes: {
        'GET /api/auth/me': json(user()),
        'GET /api/users': json([]),
        'GET /api/locations': json([]),
      },
    });

    signIn();

    expect(await screen.findByText('Brak pracowników.')).toBeTruthy();
    expect(router.state.location.pathname).toBe('/company/staff');
  });

  test('kitchen user starts in the kitchen and cannot open the company panel', async () => {
    const { router } = renderApp({
      path: '/company/locations',
      signedIn: true,
      routes: { 'GET /api/auth/me': json(user({ roles: ['Kitchen'], locationIds: ['l-1'] })) },
    });

    expect(await screen.findByRole('heading', { name: 'Kuchnia' })).toBeTruthy();
    expect(router.state.location.pathname).toBe('/kitchen');
    expect(screen.queryByRole('link', { name: 'Lokale' })).toBeNull();
  });

  test('logging out returns to the login screen', async () => {
    renderApp({
      signedIn: true,
      routes: { 'GET /api/auth/me': json(user()), 'GET /api/locations': json([]) },
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Wyloguj' }));

    expect(await screen.findByRole('heading', { name: 'Zaloguj się' })).toBeTruthy();
  });
});
