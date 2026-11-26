import { errorCodeOf, type DriverShiftDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, SelectField, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { useLocations } from '../company/queries';
import { useFormErrors } from '../lib/formErrors';
import { parsePrice, priceInputValue } from '../menu/prices';
import { useLocationShifts, useSettleShift } from './shifts';

const time = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('pl-PL', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : '…';

/** Manager: count the cash drivers hand over and settle their shifts. */
export function ShiftsPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const locations = useLocations();
  const [picked, setPicked] = useState('');
  const locationId = picked || (locations.data?.[0]?.id ?? '');
  const waiting = useLocationShifts(locationId, 'Closed');
  const settled = useLocationShifts(locationId, 'Settled');

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-3xl font-bold">{t('shifts.title')}</h1>
        {(locations.data?.length ?? 0) > 1 && (
          <SelectField label={t('orders.location')} value={locationId} onChange={(e) => setPicked(e.target.value)}>
            {locations.data?.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </SelectField>
        )}
      </div>
      {(waiting.error ?? settled.error) && <Alert>{codes.error(errorCodeOf(waiting.error ?? settled.error))}</Alert>}

      <h2 className="text-2xl font-bold">{t('shifts.waiting')}</h2>
      {waiting.data?.length === 0 && <p className="text-lg">{t('shifts.none')}</p>}
      {waiting.data?.map((shift) => <SettleCard key={shift.id} shift={shift} locationId={locationId} />)}

      <h2 className="text-2xl font-bold">{t('shifts.settled')}</h2>
      <ul className="flex flex-col gap-2">
        {settled.data?.map((shift) => (
          <li key={shift.id} className="flex flex-wrap justify-between gap-2 rounded-lg bg-white px-4 py-3 shadow-sm">
            <span className="font-semibold">{t('shifts.driverRange', { driver: shift.driverName ?? '?', from: time(shift.startedAt), to: time(shift.endedAt) })}</span>
            <span className={shift.discrepancy ? 'font-semibold text-red-800' : 'text-green-800'}>
              {shift.discrepancy ? t('shifts.discrepancy', { amount: formatMoney(shift.discrepancy) }) : t('shifts.noDiscrepancy')}
              {shift.discrepancyComment && ` — ${shift.discrepancyComment}`}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function SettleCard({ shift, locationId }: { shift: DriverShiftDto; locationId: string }) {
  const { t } = useTranslation();
  const settle = useSettleShift(locationId);
  const errors = useFormErrors(settle.error);
  const [counted, setCounted] = useState(priceInputValue(shift.expectedCash));
  const [comment, setComment] = useState('');
  const countedValue = parsePrice(counted);
  const difference = countedValue !== null && !Number.isNaN(countedValue) ? countedValue - shift.expectedCash : null;

  return (
    <article aria-label={shift.driverName ?? shift.driverId} className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm">
      <p className="text-xl font-semibold">{t('shifts.driverRange', { driver: shift.driverName ?? '?', from: time(shift.startedAt), to: time(shift.endedAt) })}</p>
      <dl className="grid grid-cols-2 gap-x-6 gap-y-1 text-lg sm:grid-cols-3">
        <div><dt className="text-sm text-neutral-600">{t('shifts.deliveries')}</dt><dd>{shift.deliveriesCount}</dd></div>
        <div><dt className="text-sm text-neutral-600">{t('shifts.startingCash')}</dt><dd>{formatMoney(shift.startingCash)}</dd></div>
        <div><dt className="text-sm text-neutral-600">{t('shifts.cash')}</dt><dd>{formatMoney(shift.cashCollected)}</dd></div>
        <div><dt className="text-sm text-neutral-600">{t('shifts.card')}</dt><dd>{formatMoney(shift.cardCollected)}</dd></div>
        <div><dt className="text-sm text-neutral-600">{t('shifts.tipsTotal')}</dt><dd>{formatMoney(shift.tips)}</dd></div>
        <div><dt className="text-sm text-neutral-600">{t('shifts.toHandOver')}</dt><dd className="font-bold">{formatMoney(shift.expectedCash)}</dd></div>
      </dl>
      {errors.general && <Alert>{errors.general}</Alert>}
      <div className="grid grid-cols-1 gap-3 md:grid-cols-[12rem_1fr_auto] md:items-end">
        <TextField label={t('shifts.countedCash')} inputMode="decimal" value={counted} onChange={(e) => setCounted(e.target.value)} error={errors.field('countedCash')} />
        <TextField label={t('shifts.comment')} value={comment} onChange={(e) => setComment(e.target.value)} />
        <Button
          disabled={settle.isPending || difference === null}
          onClick={() => settle.mutate({ id: shift.id, countedCash: countedValue!, comment: comment.trim() || null })}
        >
          {t('shifts.settle')}
        </Button>
      </div>
      {difference !== null && difference !== 0 && (
        <p className="text-lg font-semibold text-red-800">{t('shifts.discrepancy', { amount: formatMoney(difference) })}</p>
      )}
    </article>
  );
}
