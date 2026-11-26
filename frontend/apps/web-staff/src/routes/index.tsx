import { Roles } from '@pizzeria/api-client';
import { Navigate, type RouteObject } from 'react-router';
import { LoginPage } from '../auth/LoginPage';
import { RequireAuth, RequireRole } from '../auth/RequireAuth';
import { EditLocationPage, NewLocationPage } from '../company/LocationFormPage';
import { LocationsPage } from '../company/LocationsPage';
import { NewStaffPage } from '../company/NewStaffPage';
import { StaffPage } from '../company/StaffPage';
import { DriverPage } from '../driver/DriverPage';
import { ShiftsPage } from '../driver/ShiftsPage';
import { KitchenPage } from '../kitchen/KitchenPage';
import { EditCategoryPage, NewCategoryPage } from '../menu/CategoryFormPage';
import { MenuPage } from '../menu/MenuPage';
import { EditModifierGroupPage, NewModifierGroupPage } from '../menu/ModifierGroupFormPage';
import { EditProductPage, NewProductPage } from '../menu/ProductFormPage';
import { OrdersPage } from '../orders/OrdersPage';
import { PhoneOrderPage } from '../orders/PhoneOrderPage';
import { ZoneFormPage } from '../zones/ZoneFormPage';
import { ZonesPage } from '../zones/ZonesPage';
import { navItems } from './navigation';
import { Layout, StartPage } from './Layout';

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
        element: <RequireRole roles={rolesFor('/orders')} />,
        children: [
          { index: true, element: <OrdersPage /> },
          { path: 'new', element: <PhoneOrderPage /> },
        ],
      },
      {
        path: 'kitchen',
        element: (
          <RequireRole roles={rolesFor('/kitchen')}>
            <KitchenPage />
          </RequireRole>
        ),
      },
      {
        path: 'driver',
        element: (
          <RequireRole roles={rolesFor('/driver')}>
            <DriverPage />
          </RequireRole>
        ),
      },
      {
        path: 'shifts',
        element: (
          <RequireRole roles={rolesFor('/shifts')}>
            <ShiftsPage />
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
        // Zones are also managed by location managers (the API limits them to their own locations).
        path: 'company/locations/:locationId/zones',
        element: <RequireRole roles={[Roles.CompanyAdmin, Roles.LocationManager]} />,
        children: [
          { index: true, element: <ZonesPage /> },
          { path: 'new', element: <ZoneFormPage /> },
          { path: ':zoneId', element: <ZoneFormPage /> },
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
