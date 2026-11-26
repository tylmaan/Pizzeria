import { errorCodeOf, Roles, type OrderStatus, type StaffOrderDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, SelectField } from '@pizzeria/ui';
import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { hasAnyRole, useCurrentUser } from '../auth/session';
import { useLocations } from '../company/queries';
import { OrderCard } from './OrderCard';

// Leaflet is only loaded when someone opens the map.
const LiveMap = lazy(() => import('./LiveMap').then((m) => ({ default: m.LiveMap })));
import { useCurrentLocationId, useNow, useOrders, useOrdersLive } from './queries';

const columns: { key: 'New' | 'InProgress' | 'Ready' | 'OnTheWay'; statuses: OrderStatus[] }[] = [
  { key: 'New', statuses: ['New'] },
  { key: 'InProgress', statuses: ['Accepted', 'InPreparation'] },
  { key: 'Ready', statuses: ['Ready', 'AssignedToDriver'] },
  { key: 'OnTheWay', statuses: ['InDelivery'] },
];

/** The live order queue of a location: new orders on the left, ready for pickup or delivery on the right. */
export function OrdersPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const navigate = useNavigate();
  const user = useCurrentUser();
  const [locationId, setLocationId] = useCurrentLocationId();
  const locations = useLocations();
  const live = useOrdersLive(locationId, { alertOn: 'New' });
  const orders = useOrders(locationId, 'Active', live);
  const now = useNow();
  const canCancel = hasAnyRole(user, [Roles.CompanyAdmin, Roles.LocationManager, Roles.Staff]);
  const [showMap, setShowMap] = useState(false);
  const location = locations.data?.find((l) => l.id === locationId);

  // "N" opens a new phone order — the most frequent action, reachable without the mouse.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const typing = event.target instanceof HTMLElement && ['INPUT', 'SELECT', 'TEXTAREA'].includes(event.target.tagName);
      if (!typing && event.key.toLowerCase() === 'n' && !event.ctrlKey && !event.metaKey && !event.altKey) {
        void navigate('/orders/new');
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [navigate]);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-3xl font-bold">{t('orders.title')}</h1>
          <span className={`rounded-full px-3 py-1 text-sm font-semibold ${live === 'connected' ? 'bg-green-100 text-green-900' : 'bg-yellow-100 text-yellow-900'}`}>
            {live === 'connected' ? t('orders.live') : t('orders.offline')}
          </span>
        </div>
        <div className="flex items-end gap-3">
          {user.locationIds.length > 1 && (
            <SelectField label={t('orders.location')} value={locationId} onChange={(e) => setLocationId(e.target.value)}>
              {locations.data?.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
            </SelectField>
          )}
          <Link to="/orders/new" className={buttonClassName()} aria-keyshortcuts="N">
            {t('orders.newOrder')} (N)
          </Link>
        </div>
      </div>

      {orders.isError && <Alert>{codes.error(errorCodeOf(orders.error))}</Alert>}

      <Button variant="secondary" className="self-start" aria-expanded={showMap} onClick={() => setShowMap((v) => !v)}>
        {showMap ? t('liveMap.hide') : t('liveMap.show')}
      </Button>
      {showMap && (
        <Suspense fallback={<p>{t('common.loading')}</p>}>
          <LiveMap locationId={locationId} orders={orders.data ?? []} center={location?.position ?? null} />
        </Suspense>
      )}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        {columns.map((column) => {
          const inColumn = (orders.data ?? []).filter((o: StaffOrderDto) => column.statuses.includes(o.status));
          return (
            <section key={column.key} aria-label={t(`orders.columns.${column.key}`)} className="flex flex-col gap-3 rounded-xl bg-neutral-200 p-3">
              <h2 className="text-xl font-bold">
                {t(`orders.columns.${column.key}`)} ({inColumn.length})
              </h2>
              {inColumn.length === 0 && <p className="text-base text-neutral-600">{t('orders.empty')}</p>}
              {inColumn.map((order) => (
                <OrderCard key={order.id} order={order} now={now} canCancel={canCancel} />
              ))}
            </section>
          );
        })}
      </div>
    </section>
  );
}
