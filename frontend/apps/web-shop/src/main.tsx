import { initI18n } from '@pizzeria/i18n';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { createShopServices, ShopServicesProvider } from './app/services';
import './index.css';
import { routes } from './routes';

initI18n();

const services = createShopServices();
const router = createBrowserRouter(routes);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ShopServicesProvider services={services}>
      <RouterProvider router={router} />
    </ShopServicesProvider>
  </StrictMode>,
);
