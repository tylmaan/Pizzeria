import type { RouteObject } from 'react-router';
import { CartPage } from './pages/CartPage';
import { CheckoutPage } from './pages/CheckoutPage';
import { FakePayPage } from './pages/FakePayPage';
import { Layout } from './pages/Layout';
import { MenuPage } from './pages/MenuPage';
import { ProductPage } from './pages/ProductPage';
import { TrackingPage } from './pages/TrackingPage';

export const routes: RouteObject[] = [
  {
    element: <Layout />,
    children: [
      { index: true, element: <MenuPage /> },
      { path: 'product/:productId', element: <ProductPage /> },
      { path: 'cart', element: <CartPage /> },
      { path: 'checkout', element: <CheckoutPage /> },
      { path: 'order/:token', element: <TrackingPage /> },
      { path: 'pay/fake/:providerPaymentId', element: <FakePayPage /> },
    ],
  },
];
