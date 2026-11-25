import { Roles } from '@pizzeria/api-client';
import { Navigate, type RouteObject } from 'react-router';
import { LoginPage } from '../auth/LoginPage';
import { RequireAuth, RequireRole } from '../auth/RequireAuth';
import { EditLocationPage, NewLocationPage } from '../company/LocationFormPage';
import { LocationsPage } from '../company/LocationsPage';
import { NewStaffPage } from '../company/NewStaffPage';
import { StaffPage } from '../company/StaffPage';
import { EditCategoryPage, NewCategoryPage } from '../menu/CategoryFormPage';
import { MenuPage } from '../menu/MenuPage';
import { EditModifierGroupPage, NewModifierGroupPage } from '../menu/ModifierGroupFormPage';
import { EditProductPage, NewProductPage } from '../menu/ProductFormPage';
import { navItems } from './navigation';
import { Layout, StartPage } from './Layout';
import { Placeholder } from './Placeholder';

const rolesFor = (to: string) => navItems.find((item) => item.to === to)?.roles ?? [];

// One PWA, separate route sections per role: location panel, kitchen, driver, company panel.
export const routes: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  {
    element: (
      <RequireAuth>
        <Layout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <StartPage /> },
      {
        path: 'orders',
        element: (
          <RequireRole roles={rolesFor('/orders')}>
            <Placeholder titleKey="nav.orders" />
          </RequireRole>
        ),
      },
      {
        path: 'kitchen',
        element: (
          <RequireRole roles={rolesFor('/kitchen')}>
            <Placeholder titleKey="nav.kitchen" />
          </RequireRole>
        ),
      },
      {
        path: 'driver',
        element: (
          <RequireRole roles={rolesFor('/driver')}>
            <Placeholder titleKey="nav.driver" />
          </RequireRole>
        ),
      },
      {
        path: 'company/locations',
        element: <RequireRole roles={[Roles.CompanyAdmin]} />,
        children: [
          { index: true, element: <LocationsPage /> },
          { path: 'new', element: <NewLocationPage /> },
          { path: ':id', element: <EditLocationPage /> },
        ],
      },
      {
        path: 'company/menu',
        element: <RequireRole roles={[Roles.CompanyAdmin]} />,
        children: [
          { index: true, element: <MenuPage /> },
          { path: 'categories/new', element: <NewCategoryPage /> },
          { path: 'categories/:id', element: <EditCategoryPage /> },
          { path: 'products/new', element: <NewProductPage /> },
          { path: 'products/:id', element: <EditProductPage /> },
          { path: 'modifier-groups/new', element: <NewModifierGroupPage /> },
          { path: 'modifier-groups/:id', element: <EditModifierGroupPage /> },
        ],
      },
      {
        path: 'company/staff',
        element: <RequireRole roles={[Roles.CompanyAdmin, Roles.LocationManager]} />,
        children: [
          { index: true, element: <StaffPage /> },
          { path: 'new', element: <NewStaffPage /> },
        ],
      },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
];
