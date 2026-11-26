import { errorCodeOf, type OrderStatus } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import { useParams } from 'react-router';
import { useTracking } from '../app/services';

const deliverySteps: OrderStatus[] = ['New', 'Accepted', 'InPreparation', 'Ready', 'InDelivery', 'Delivered'];
const pickupSteps: OrderStatus[] = ['New', 'Accepted', 'InPreparation', 'Ready', 'PickedUp'];

export function TrackingPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const { token = '' } = useParams();
  const tracking = useTracking(token);

  if (tracking.isError) {
    return <Alert>{codes.error(errorCodeOf(tracking.error))}</Alert>;
  }
  const order = tracking.data;
  if (order === undefined) {
    return <p className="text-lg">{t('common.loading')}</p>;
  }

  const steps = order.fulfillment === 'Delivery' ? deliverySteps : pickupSteps;
  // "Waiting for a driver" is shown as part of "Ready" to the customer.
  const shownStatus: OrderStatus = order.status === 'AssignedToDriver' ? 'Ready' : order.status;
  const currentIndex = steps.indexOf(shownStatus);

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="text-3xl font-bold">{t('shop.orderTitle', { number: order.number })}</h1>
        <p className="text-lg text-neutral-700">{t('shop.orderFrom', { location: order.locationName })}</p>
        <p className="text-sm text-neutral-600">{t('shop.trackingHint')}</p>
      </div>

      {order.status === 'Cancelled' ? (
        <Alert>{t('shop.cancelled', { reason: order.cancellationReason ?? '' })}</Alert>
      ) : (
        <ol className="flex flex-col gap-2" aria-label={t('orderStatus.New')}>
          {steps.map((step, index) => (
            <li
              key={step}
              aria-current={index === currentIndex ? 'step' : undefined}
              className={`flex min-h-12 items-center gap-3 rounded-xl px-4 text-lg ${
                index < currentIndex ? 'bg-green-50 text-green-900' : index === currentIndex ? 'bg-orange-600 font-bold text-white' : 'bg-white text-neutral-500'
              }`}
            >
              <span aria-hidden="true">{index < currentIndex ? '✓' : index + 1}</span>
              {t(`orderStatus.${step}`)}
            </li>
          ))}
        </ol>
      )}

      <ul className="flex flex-col gap-1 rounded-xl bg-white p-3 shadow-sm">
        {order.items.map((item, index) => (
          <li key={index} className="flex justify-between gap-3 text-base">
            <span>
              <strong>{item.quantity}×</strong> {item.productName}
              {item.secondHalfProductName && ` / ${item.secondHalfProductName}`}, {item.sizeName}
              {item.modifiers.length > 0 && <span className="text-neutral-600"> ({item.modifiers.map((m) => m.name).join(', ')})</span>}
            </span>
            <span className="whitespace-nowrap">{formatMoney(item.lineTotal)}</span>
          </li>
        ))}
      </ul>
      <dl className="flex flex-col gap-1 text-lg">
        <div className="flex justify-between"><dt>{t('shop.itemsTotal')}</dt><dd>{formatMoney(order.itemsTotal)}</dd></div>
        {order.fulfillment === 'Delivery' && (
          <div className="flex justify-between"><dt>{t('shop.deliveryFee')}</dt><dd>{formatMoney(order.deliveryFee)}</dd></div>
        )}
        <div className="flex justify-between text-xl font-bold"><dt>{t('shop.total')}</dt><dd>{formatMoney(order.total)}</dd></div>
        <div className="flex justify-between"><dt>{t('shop.payment')}</dt><dd>{t(`paymentMethod.${order.paymentMethod}`)}</dd></div>
      </dl>
    </div>
  );
}
