import { errorCodeOf, type StaffOrderDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import { useAdvanceOrder, useCurrentLocationId, useOrders, useOrdersLive } from '../orders/queries';
import { useGpsReporter } from './gps';
import { ShiftPanel } from './ShiftPanel';
import { useCurrentShift } from './shifts';

/** Google Maps directions: by coordinates when the address was geocoded, by the address text otherwise. */
export function navigationUrl(order: StaffOrderDto): string {
  const destination = order.deliveryPoint
    ? `${order.deliveryPoint.latitude},${order.deliveryPoint.longitude}`
    : `${order.deliveryAddress?.street ?? ''}, ${order.deliveryAddress?.postalCode ?? ''} ${order.deliveryAddress?.city ?? ''}`;
  return `https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=${encodeURIComponent(destination)}`;
}

/**
 * Driver app (phone, one hand): the driver's own deliveries, biggest buttons at the bottom.
 * Live GPS tracking comes in stage 2.
 */
export function DriverPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const [locationId] = useCurrentLocationId();
  const live = useOrdersLive(locationId, { alertOn: 'AssignedToDriver' });
  const orders = useOrders(locationId, 'Active', live);

  // The API returns only this driver's orders; on the way first, then waiting ones in assignment order.
  const runs = (orders.data ?? [])
    .filter((o) => o.status === 'AssignedToDriver' || o.status === 'InDelivery')
    .sort((a, b) => (a.status === b.status ? Date.parse(a.statusChangedAt) - Date.parse(b.statusChangedAt) : a.status === 'InDelivery' ? -1 : 1));

  return (
    <section className="mx-auto flex max-w-md flex-col gap-4">
      <h1 className="text-3xl font-bold">{t('driver.title')}</h1>
      <ShiftPanel locationId={locationId} />
      <GpsBanner />
      <p className="text-sm text-neutral-600">{t('driver.keepOpen')}</p>
      {orders.isError && <Alert>{codes.error(errorCodeOf(orders.error))}</Alert>}
      {orders.data !== undefined && runs.length === 0 && <p className="text-xl">{t('driver.empty')}</p>}
      {runs.map((order) => (
        <DriverRun key={order.id} order={order} />
      ))}
    </section>
  );
}

/** GPS runs only while the shift is open (we never track drivers off duty). */
function GpsBanner() {
  const { t } = useTranslation();
  const shift = useCurrentShift();
  const gps = useGpsReporter(Boolean(shift.data?.shift));
  if (gps === 'off') {
    return null;
  }
  const good = gps === 'ok';
  return (
    <p role="status" className={`rounded-lg px-3 py-2 text-base font-semibold ${good ? 'bg-green-100 text-green-900' : 'bg-red-100 text-red-900'}`}>
      {t(`gps.${gps}`)}
    </p>
  );
}

function DriverRun({ order }: { order: StaffOrderDto }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const advance = useAdvanceOrder(order.locationId);
  const toCollect = order.paymentStatus !== 'Paid' && order.paymentMethod !== 'Online';
  const phone = order.customerPhone.replace(/\s/g, '');

  return (
    <article aria-label={`#${order.number}`} className="flex flex-col gap-3 rounded-2xl border-2 border-neutral-300 bg-white p-4 shadow">
      <header className="flex items-center justify-between">
        <span className="text-3xl font-black">#{order.number}</span>
        <span className={`rounded-full px-3 py-1 text-base font-bold ${order.status === 'InDelivery' ? 'bg-blue-100 text-blue-900' : 'bg-yellow-100 text-yellow-900'}`}>
          {order.status === 'InDelivery' ? t('driver.onTheWay') : t('driver.waiting')}
        </span>
      </header>

      {order.deliveryAddress && (
        <p className="text-2xl font-bold leading-tight">
          {order.deliveryAddress.street}
          <span className="block text-lg font-normal">
            {order.deliveryAddress.postalCode} {order.deliveryAddress.city}
          </span>
          {order.deliveryNotes && <span className="block text-lg font-semibold text-red-800">{order.deliveryNotes}</span>}
        </p>
      )}
      <p className="text-lg">{order.customerName}</p>

      <p className={`rounded-lg px-3 py-2 text-xl font-bold ${toCollect ? 'bg-orange-100 text-orange-900' : 'bg-green-100 text-green-900'}`}>
        {toCollect
          ? t('driver.collect', { amount: formatMoney(order.total), method: t(`paymentMethod.${order.paymentMethod}`) })
          : t('driver.paidOnline')}
      </p>

      <div className="grid grid-cols-2 gap-2">
        <a href={`tel:${phone}`} className="flex min-h-14 items-center justify-center rounded-xl bg-neutral-200 text-lg font-bold">
          {t('driver.call')}
        </a>
        <a href={navigationUrl(order)} target="_blank" rel="noreferrer" className="flex min-h-14 items-center justify-center rounded-xl bg-neutral-200 text-lg font-bold">
          {t('driver.navigate')}
        </a>
      </div>

      {advance.isError && <Alert>{codes.error(errorCodeOf(advance.error))}</Alert>}
      <button
        type="button"
        disabled={advance.isPending}
        onClick={() => advance.mutate(order.id)}
        className={`min-h-16 rounded-xl text-2xl font-black text-white disabled:opacity-50 ${order.status === 'InDelivery' ? 'bg-green-700 active:bg-green-800' : 'bg-orange-600 active:bg-orange-700'}`}
      >
        {order.status === 'InDelivery' ? t('driver.delivered') : t('driver.pickedUp')}
      </button>
    </article>
  );
}
