import { initI18n } from '@pizzeria/i18n';
import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { expect, test } from 'vitest';
import { routes } from '.';

initI18n();

test('kitchen route renders translated heading and navigation', () => {
  render(<RouterProvider router={createMemoryRouter(routes, { initialEntries: ['/kitchen'] })} />);

  expect(screen.getByRole('heading', { name: 'Kuchnia' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Kierowca' })).toBeTruthy();
});
