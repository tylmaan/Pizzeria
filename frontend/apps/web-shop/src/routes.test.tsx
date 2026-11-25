import { initI18n } from '@pizzeria/i18n';
import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { expect, test } from 'vitest';
import { routes } from './routes';

initI18n();

test('home renders translated heading', () => {
  render(<RouterProvider router={createMemoryRouter(routes)} />);

  expect(screen.getByRole('heading', { name: 'Zamów pizzę' })).toBeTruthy();
});
