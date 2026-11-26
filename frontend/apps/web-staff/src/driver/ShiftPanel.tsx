import { errorCodeOf, type DriverShiftDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { parsePrice } from '../menu/prices';
import { useCloseShift, useCurrentShift, useStartShift } from './shifts';

const time = (iso: string) => new Date(iso).toLocaleTimeString('pl-PL', { hour: '2-digit', minute: '2-digit' });

/** Start / end of the driver's shift, with the cash summary to hand over at the end. */
export function ShiftPanel({ locationId }: { locationId: string }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const current = useCurrentShift();
  const start = useStartShift();
  const close = useCloseShift();
  const [startingCash, setStartingCash] = useState('');
  const [ending, setEnding] = useState(false);
  const [tips, setTips] = useState('');
  const [summary, setSummary] = useState<DriverShiftDto | null>(null);

  const error = start.error ?? close.error;
  const shift = current.data?.shift;

  if (summary) {
    return <ShiftSummary shift={summary} onClose={() => setSummary(null)} />;
  }
  if (current.data === undefined) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-neutral-900 p-4 text-white">
      {error && <Alert>{codes.error(errorCodeOf(error))}</Alert>}
      {shift ? (
        ending ? (
          <>
            <TextField label={t('shifts.tips')} inputMode="decimal" value={tips} onChange={(e) => setTips(e.target.value)} className="text-neutral-900" />
            <div className="grid grid-cols-2 gap-2">
              <Button variant="secondary" onClick={() => setEnding(false)}>
                {t('common.cancel')}
              </Button>
              <Button
                disabled={close.isPending}
                onClick={() => close.mutate(parsePrice(tips) ?? 0, { onSuccess: (closed) => { setSummary(closed); setEnding(false); setTips(''); } })}
              >
                {t('shifts.confirmEnd')}
              </Button>
            </div>
          </>
        ) : (
          <div className="flex items-center justify-between gap-3">
            <span className="text-lg">{t('shifts.openSince', { time: time(shift.startedAt), cash: formatMoney(shift.startingCash) })}</span>
            <Button variant="secondary" onClick={() => setEnding(true)}>
              {t('shifts.end')}
            </Button>
          </div>
        )
      ) : (
        <>
          <p className="text-lg font-semibold">{t('shifts.notStarted')}</p>
          <TextField label={t('shifts.startingCash')} inputMode="decimal" value={startingCash} onChange={(e) => setStartingCash(e.target.value)} className="text-neutral-900" />
          <Button
            className="min-h-14 text-xl"
            disabled={start.isPending || Number.isNaN(parsePrice(startingCash) ?? 0)}
            onClick={() => start.mutate({ locationId, startingCash: parsePrice(startingCash) ?? 0 })}
          >
            {t('shifts.start')}
          </Button>
        </>
      )}
    </section>
  );
}

function ShiftSummary({ shift, onClose }: { shift: DriverShiftDto; onClose: () => void }) {
  const { t } = useTranslation();
  const rows: [string, string][] = [
    [t('shifts.deliveries'), String(shift.deliveriesCount)],
    [t('shifts.cash'), formatMoney(shift.cashCollected)],
    [t('shifts.card'), formatMoney(shift.cardCollected)],
    [t('shifts.online'), formatMoney(shift.paidOnline)],
    [t('shifts.tipsTotal'), formatMoney(shift.tips)],
  ];
  return (
    <section aria-label={t('shifts.summary')} className="flex flex-col gap-2 rounded-2xl border-4 border-green-600 bg-white p-4">
      <h2 className="text-2xl font-bold">{t('shifts.summary')}</h2>
      <dl className="flex flex-col gap-1 text-lg">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between">
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
        <div className="flex justify-between text-2xl font-black">
          <dt>{t('shifts.toHandOver')}</dt>
          <dd>{formatMoney(shift.expectedCash)}</dd>
        </div>
      </dl>
      <Button onClick={onClose}>{t('shifts.close')}</Button>
    </section>
  );
}
