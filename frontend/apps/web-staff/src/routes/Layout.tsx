import { useTranslation } from '@pizzeria/i18n';
import { NavLink, Outlet } from 'react-router';

const links = [
  { to: '/', key: 'nav.orders' },
  { to: '/kitchen', key: 'nav.kitchen' },
  { to: '/driver', key: 'nav.driver' },
  { to: '/company', key: 'nav.company' },
] as const;

export function Layout() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center gap-4 bg-neutral-900 px-4 text-white">
        <span className="text-xl font-bold">{t('app.staffTitle')}</span>
        <nav className="flex gap-1">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end
              className={({ isActive }) =>
                `flex min-h-12 items-center px-4 text-lg ${isActive ? 'bg-orange-600' : 'hover:bg-neutral-700'}`
              }
            >
              {t(link.key)}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="flex-1 p-4">
        <Outlet />
      </main>
    </div>
  );
}
