import { errorCodeOf, type StaffOrderDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import { useAdvanceOrder, useCurrentLocationId, useNow, useOrders, useOrdersLive } from '../orders/queries';

/** Minutes since acceptance after which a tile turns yellow / red. */
export const KITCHEN_WARNING_MINUTES = 10;
export const KITCHEN_LATE_MINUTES = 20;

type Age = 'fresh' | 'warning' | 'late';

const ageClasses: Record<Age, string> = {
  fresh: 'border-green-500 bg-green-950',
  warning: 'border-yellow-400 bg-yellow-950',
  late: 'border-red-500 bg-red-950',
};

export function ageOf(minutes: number): Age {
  if (minutes >= KITCHEN_LATE_MINUTES) {
    return 'late';
  }
  return minutes >= KITCHEN_WARNING_MINUTES ? 'warning' : 'fresh';
}

const since = (order: StaffOrderDto) => Date.parse(order.acceptedAt ?? order.placedAt);

/**
 * Kitchen display: accepted orders, oldest first, readable from across the kitchen. One tap = next step.
 * Rings when staff accepts an order (that is when it reaches the kitchen).
 */
export function KitchenPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const [locationId] = useCurrentLocationId();
  const live = useOrdersLive(locationId, { alertOn: 'Accepted' });
  const orders = useOrders(locationId, 'Active', live);
  const now = useNow(15_000);

  const queue = (orders.data ?? [])
    .filter((o) => o.status === 'Accepted' || o.status === 'InPreparation')
    .sort((a, b) => since(a) - since(b));

  return (
    <section className="-m-4 min-h-[calc(100vh-4rem)] bg-neutral-950 p-4 text-white">
      <h1 className="sr-only">{t('kitchen.title')}</h1>
      {orders.isError && <Alert>{codes.error(errorCodeOf(orders.error))}</Alert>}
      {queue.length === 0 && <p className="p-8 text-center text-3xl text-neutral-400">{t('kitchen.empty')}</p>}
      <ol className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {queue.map((order) => (
          <KitchenTile key={order.id} order={order} minutes={Math.max(0, Math.floor((now - since(order)) / 60_000))} />
        ))}
      </ol>
    </section>
  );
}

function KitchenTile({ order, minutes }: { order: StaffOrderDto; minutes: number }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const advance = useAdvanceOrder(order.locationId);
  const age = ageOf(minutes);

  return (
    <li aria-label={`#${order.number}`} data-age={age} className={`flex flex-col gap-3 rounded-2xl border-4 p-4 ${ageClasses[age]}`}>
      <header className="flex items-baseline justify-between">
        <span className="text-5xl font-black">#{order.number}</span>
        <span className="text-3xl font-bold">{t('kitchen.since', { minutes })}</span>
      </header>
      <ul className="flex flex-col gap-2 text-2xl">
        {order.items.map((item, index) => (
          <li key={index}>
            <span className="font-black">{item.quantity}×</span> {item.productName}
            {item.secondHalfProductName && ` / ${item.secondHalfProductName}`} <span className="font-bold">{item.sizeName}</span>
            {item.modifiers.length > 0 && <span className="block text-xl text-neutral-200">+ {item.modifiers.map((m) => m.name).join(', ')}</span>}
            {item.notes && <span className="block text-xl font-bold text-yellow-300">! {item.notes}</span>}
          </li>
        ))}
      </ul>
      {order.notes && <p className="text-xl font-bold text-yellow-300">! {order.notes}</p>}
      {advance.isError && <Alert>{codes.error(errorCodeOf(advance.error))}</Alert>}
      <button
        type="button"
        disabled={advance.isPending}
        onClick={() => advance.mutate(order.id)}
        className="min-h-20 rounded-xl bg-white text-3xl font-black text-neutral-950 active:bg-neutral-300 disabled:opacity-50"
      >
        {order.status === 'Accepted' ? t('kitchen.start') : t('kitchen.ready')}
      </button>
    </li>
  );
}
