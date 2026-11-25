import { useTranslation } from '@pizzeria/i18n';
import { Button } from '@pizzeria/ui';
import { Navigate, NavLink, Outlet } from 'react-router';
import { useServices } from '../app/services';
import { useCurrentUser } from '../auth/session';
import { navItemsFor } from './navigation';

export function Layout() {
  const { t } = useTranslation();
  const { session } = useServices();
  const user = useCurrentUser();
  const items = navItemsFor(user.roles);

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex flex-wrap items-center gap-x-4 bg-neutral-900 px-4 text-white">
        <span className="py-3 text-xl font-bold">{t('app.staffTitle')}</span>
        <nav aria-label={t('nav.mainMenu')} className="flex flex-1 flex-wrap gap-1">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex min-h-12 items-center px-4 text-lg ${isActive ? 'bg-orange-600' : 'hover:bg-neutral-700'}`
              }
            >
              {t(item.labelKey)}
            </NavLink>
          ))}
        </nav>
        <div className="flex items-center gap-3 py-2">
          <span className="text-base">{user.displayName}</span>
          <Button variant="secondary" onClick={() => session.logout()}>
            {t('auth.logout')}
          </Button>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 p-4">
        <Outlet />
      </main>
    </div>
  );
}

/** `/` → the first section the user may open. */
export function StartPage() {
  const { t } = useTranslation();
  const [first] = navItemsFor(useCurrentUser().roles);
  return first ? <Navigate to={first.to} replace /> : <p className="text-lg">{t('placeholder.noPanel')}</p>;
}
