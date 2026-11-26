import { errorCodeOf, type StaffOrderDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, SelectField, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { useReprintTicket } from '../printing/queries';
import { useAdvanceOrder, useAssignDriver, useCancelOrder, useDrivers } from './queries';

export function OrderCard({ order, now, canCancel }: { order: StaffOrderDto; now: number; canCancel: boolean }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const advance = useAdvanceOrder(order.locationId);
  const cancel = useCancelOrder(order.locationId);
  const assign = useAssignDriver(order.locationId);
  const needsDriver = order.fulfillment === 'Delivery' && (order.status === 'Ready' || order.status === 'AssignedToDriver');
  const drivers = useDrivers(order.locationId, needsDriver);
  const [driverId, setDriverId] = useState(order.driverId ?? '');
  const [cancelling, setCancelling] = useState(false);
  const [reason, setReason] = useState('');

  const minutes = Math.max(0, Math.floor((now - Date.parse(order.placedAt)) / 60_000));
  const error = advance.error ?? cancel.error ?? assign.error;
  const driverName = drivers.data?.find((d) => d.id === order.driverId)?.displayName;

  return (
    <article aria-label={`#${order.number}`} className="flex flex-col gap-2 rounded-xl border-2 border-neutral-300 bg-white p-3 shadow-sm">
      <header className="flex items-center justify-between gap-2">
        <span className="text-2xl font-bold">#{order.number}</span>
        <span className={`text-lg font-semibold ${minutes >= 30 ? 'text-red-700' : 'text-neutral-700'}`}>
          {t('orders.minutesAgo', { minutes })}
        </span>
      </header>
      <div className="flex flex-wrap gap-2 text-sm font-semibold">
        <span className={`rounded-full px-2 py-0.5 ${order.fulfillment === 'Delivery' ? 'bg-blue-100 text-blue-900' : 'bg-purple-100 text-purple-900'}`}>
          {order.fulfillment === 'Delivery' ? t('orders.delivery') : t('orders.pickup')}
        </span>
        {order.source !== 'Online' && <span className="rounded-full bg-neutral-200 px-2 py-0.5">{t('orders.phone')}</span>}
        {order.paymentStatus === 'PendingOnline' && (
          <span className="rounded-full bg-amber-200 px-2 py-0.5 text-amber-950">{t('orders.awaitingPayment')}</span>
        )}
        <span className="rounded-full bg-orange-100 px-2 py-0.5 text-orange-900">{t(`staffOrderStatus.${order.status}`)}</span>
      </div>
      <p className="text-lg">
        {order.customerName} ·{' '}
        <a href={`tel:${order.customerPhone.replace(/\s/g, '')}`} className="font-semibold text-blue-800 underline">
          {order.customerPhone}
        </a>
      </p>
      {order.deliveryAddress && (
        <p className="text-base">
          {order.deliveryAddress.street}, {order.deliveryAddress.city}
          {order.deliveryNotes && <span className="text-neutral-600"> ({order.deliveryNotes})</span>}
        </p>
      )}
      <ul className="text-base">
        {order.items.map((item, index) => (
          <li key={index}>
            <strong>{item.quantity}×</strong> {item.productName}
            {item.secondHalfProductName && ` / ${item.secondHalfProductName}`} {item.sizeName}
            {item.modifiers.length > 0 && <span className="text-neutral-700"> + {item.modifiers.map((m) => m.name).join(', ')}</span>}
            {item.notes && <span className="block text-sm italic text-red-800">{item.notes}</span>}
          </li>
        ))}
      </ul>
      {order.notes && <p className="text-base font-semibold text-red-800">{t('orders.notes', { notes: order.notes })}</p>}
      <p className="text-lg font-semibold">
        {t('orders.total', { total: formatMoney(order.total), payment: t(`paymentMethod.${order.paymentMethod}`) })}
        {order.paymentStatus === 'Paid' && <span className="ml-2 text-green-800">({t('orders.paid')})</span>}
      </p>

      {error && <Alert>{codes.error(errorCodeOf(error))}</Alert>}

      {needsDriver && (
        <div className="flex flex-col gap-2">
          {drivers.data?.length === 0 && <p className="text-base text-red-800">{t('orders.noDrivers')}</p>}
          {driverName && <p className="text-base font-semibold">{t('orders.driverAssigned', { name: driverName })}</p>}
          {(drivers.data?.length ?? 0) > 0 && order.status === 'Ready' && (
            <div className="flex items-end gap-2">
              <div className="flex-1">
                <SelectField label={t('orders.driver')} value={driverId} onChange={(e) => setDriverId(e.target.value)}>
                  <option value="" disabled>
                    —
                  </option>
                  {drivers.data?.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.displayName}
                    </option>
                  ))}
                </SelectField>
              </div>
              <Button disabled={driverId === '' || assign.isPending} onClick={() => assign.mutate({ id: order.id, driverId })}>
                {t('orders.assignDriver')}
              </Button>
            </div>
          )}
        </div>
      )}

      {order.nextStatus && (
        <Button className="w-full text-xl" disabled={advance.isPending} onClick={() => advance.mutate(order.id)}>
          {t(`orders.action.${order.nextStatus as 'Accepted'}`)}
        </Button>
      )}

      {order.status !== 'New' && order.status !== 'Cancelled' && <Reprint orderId={order.id} />}

      {canCancel &&
        (cancelling ? (
          <div className="flex flex-col gap-2 rounded-lg bg-red-50 p-2">
            <TextField label={t('orders.cancelReason')} value={reason} onChange={(e) => setReason(e.target.value)} autoFocus />
            <div className="flex gap-2">
              <Button variant="danger" disabled={reason.trim() === '' || cancel.isPending} onClick={() => cancel.mutate({ id: order.id, reason })}>
                {t('orders.confirmCancel')}
              </Button>
              <Button variant="secondary" onClick={() => setCancelling(false)}>
                {t('orders.back')}
              </Button>
            </div>
          </div>
        ) : (
          <Button variant="secondary" onClick={() => setCancelling(true)}>
            {t('orders.cancel')}
          </Button>
        ))}
    </article>
  );
}

/** Lost or unreadable kitchen ticket: print it again (needs a connected agent). */
function Reprint({ orderId }: { orderId: string }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const reprint = useReprintTicket();

  return (
    <div className="flex flex-col gap-1">
      <Button variant="secondary" disabled={reprint.isPending} onClick={() => reprint.mutate(orderId)}>
        {t('printing.reprint')}
      </Button>
      {reprint.isSuccess && <p className="text-base text-green-800" role="status">{t('printing.reprinted')}</p>}
      {reprint.error && <Alert>{codes.error(errorCodeOf(reprint.error))}</Alert>}
    </div>
  );
}
