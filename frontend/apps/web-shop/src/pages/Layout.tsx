import { errorCodeOf, previewUnitPrice } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import { Link, Outlet, useLocation } from 'react-router';
import { useSignedIn } from '../app/account';
import { useShop } from '../app/services';
import { useCart } from '../cart/cart';

export function Layout() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const shop = useShop();
  const cart = useCart();
  const path = useLocation().pathname;
  const signedIn = useSignedIn();

  if (shop.isError) {
    return (
      <main className="mx-auto max-w-xl p-4">
        <Alert>{codes.error(errorCodeOf(shop.error))}</Alert>
      </main>
    );
  }
  if (shop.data === undefined) {
    return <main className="mx-auto max-w-xl p-4 text-lg">{t('common.loading')}</main>;
  }

  const menu = shop.data.menu;
  const cartTotal = cart.items.reduce((sum, i) => sum + (previewUnitPrice(menu, i) ?? 0) * i.quantity, 0);
  const showCartBar = cart.count > 0 && (path === '/' || path.startsWith('/product'));

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center justify-between gap-3 bg-orange-600 px-4 py-3 text-white">
        <Link to="/" className="text-2xl font-bold">
          {shop.data.companyName}
        </Link>
        <Link to={signedIn ? '/account' : '/account/login'} className="flex min-h-12 items-center text-lg font-semibold underline">
          {signedIn ? t('account.title') : t('account.signIn')}
        </Link>
      </header>
      <main className={`mx-auto w-full max-w-xl flex-1 p-4 ${showCartBar ? 'pb-24' : ''}`}>
        <Outlet />
      </main>
      {showCartBar && (
        <div className="fixed inset-x-0 bottom-0 bg-white/95 p-3 shadow-[0_-2px_8px_rgba(0,0,0,0.15)]">
          <Link
            to="/cart"
            className="mx-auto flex min-h-14 max-w-xl items-center justify-center rounded-xl bg-orange-600 text-lg font-bold text-white"
          >
            {t('shop.cartButton', { count: cart.count, total: formatMoney(cartTotal) })}
          </Link>
        </div>
      )}
    </div>
  );
}
