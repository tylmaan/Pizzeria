import type { RouteObject } from 'react-router';
import { Layout } from './Layout';
import { Placeholder } from './Placeholder';

// One PWA, separate route sections per role: location panel, kitchen, driver, company panel.
export const routes: RouteObject[] = [
  {
    element: <Layout />,
    children: [
      { index: true, element: <Placeholder titleKey="nav.orders" /> },
      { path: 'kitchen', element: <Placeholder titleKey="nav.kitchen" /> },
      { path: 'driver', element: <Placeholder titleKey="nav.driver" /> },
      { path: 'company', element: <Placeholder titleKey="nav.company" /> },
    ],
  },
];
