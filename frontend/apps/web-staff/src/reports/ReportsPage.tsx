import { errorCodeOf, Roles, unwrap, type ReportDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, SelectField, TextField } from '@pizzeria/ui';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useServices } from '../app/services';
import { hasAnyRole, useCurrentUser } from '../auth/session';
import { useLocations } from '../company/queries';

const ALL = 'all';
const paymentColumns = ['Cash', 'CardOnDelivery', 'Online'] as const;

/** yyyy-mm-dd of the local calendar day, `days` back from today. */
function dayOffset(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function useReport(locationId: string, from: string, to: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: ['report', locationId, from, to],
    queryFn: () =>
      unwrap(api.GET('/api/reports', { params: { query: { locationId: locationId === ALL ? undefined : locationId, from, to } } })),
    enabled: locationId !== '' && from !== '' && to !== '',
  });
}

/** Sales reports: company admins pick a location or the whole company, managers their own locations. */
export function ReportsPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const user = useCurrentUser();
  const isAdmin = hasAnyRole(user, [Roles.CompanyAdmin]);
  const locations = useLocations();
  const [picked, setPicked] = useState('');
  const locationId = picked || (isAdmin ? ALL : (locations.data?.[0]?.id ?? ''));
  const [from, setFrom] = useState(dayOffset(6));
  const [to, setTo] = useState(dayOffset(0));
  const report = useReport(locationId, from, to);

  const range = (days: number) => {
    setFrom(dayOffset(days));
    setTo(dayOffset(0));
  };

  return (
    <section className="flex flex-col gap-5">
      <h1 className="text-3xl font-bold">{t('reports.title')}</h1>
      <div className="flex flex-wrap items-end gap-3">
        <SelectField label={t('reports.location')} value={locationId} onChange={(e) => setPicked(e.target.value)}>
          {isAdmin && <option value={ALL}>{t('reports.wholeCompany')}</option>}
          {locations.data?.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name}
            </option>
          ))}
        </SelectField>
        <TextField label={t('reports.from')} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <TextField label={t('reports.to')} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <Button variant="secondary" onClick={() => range(0)}>{t('reports.today')}</Button>
        <Button variant="secondary" onClick={() => range(6)}>{t('reports.last7')}</Button>
        <Button variant="secondary" onClick={() => range(29)}>{t('reports.last30')}</Button>
      </div>

      {report.isError && <Alert>{codes.error(errorCodeOf(report.error))}</Alert>}
      {report.data && <ReportView report={report.data} />}
    </section>
  );
}

function ReportView({ report }: { report: ReportDto }) {
  const { t } = useTranslation();
  const days = [...new Set(report.revenue.map((r) => r.day))];
  const dayTotal = (day: string) => report.revenue.filter((r) => r.day === day).reduce((sum, r) => sum + r.total, 0);
  const best = Math.max(1, ...days.map(dayTotal));
  const minutes = (value: number | null) => (value === null ? '—' : value.toFixed(1).replace('.', ','));

  return (
    <div className="flex flex-col gap-6">
      <dl className="grid grid-cols-2 gap-3 md:grid-cols-4">
        {[
          [t('reports.completed'), String(report.summary.completedOrders)],
          [t('reports.cancelled'), String(report.summary.cancelledOrders)],
          [t('reports.revenue'), formatMoney(report.summary.revenue)],
          [t('reports.average'), formatMoney(report.summary.averageOrderValue)],
        ].map(([label, value]) => (
          <div key={label} className="rounded-xl bg-white p-4 shadow-sm">
            <dt className="text-base text-neutral-600">{label}</dt>
            <dd className="text-3xl font-bold">{value}</dd>
          </div>
        ))}
      </dl>

      <section>
        <h2 className="mb-2 text-2xl font-bold">{t('reports.byDay')}</h2>
        {days.length === 0 ? (
          <p className="text-lg">{t('reports.noData')}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[36rem] border-collapse bg-white text-left text-base">
              <thead>
                <tr className="border-b-2 border-neutral-300">
                  <th className="p-2">{t('reports.day')}</th>
                  {paymentColumns.map((m) => <th key={m} className="p-2 text-right">{t(`paymentMethod.${m}`)}</th>)}
                  <th className="p-2 text-right">{t('reports.total')}</th>
                  <th className="w-1/4 p-2" aria-hidden="true" />
                </tr>
              </thead>
              <tbody>
                {days.map((day) => (
                  <tr key={day} className="border-b border-neutral-200">
                    <td className="p-2 font-semibold">{day}</td>
                    {paymentColumns.map((m) => {
                      const total = report.revenue.find((r) => r.day === day && r.paymentMethod === m)?.total ?? 0;
                      return <td key={m} className="p-2 text-right">{formatMoney(total)}</td>;
                    })}
                    <td className="p-2 text-right font-bold">{formatMoney(dayTotal(day))}</td>
                    <td className="p-2" aria-hidden="true">
                      <div className="h-3 rounded bg-orange-500" style={{ width: `${(dayTotal(day) / best) * 100}%` }} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section>
          <h2 className="mb-2 text-2xl font-bold">{t('reports.bestSellers')}</h2>
          <table className="w-full border-collapse bg-white text-left text-base">
            <thead>
              <tr className="border-b-2 border-neutral-300">
                <th className="p-2">{t('reports.product')}</th>
                <th className="p-2 text-right">{t('reports.quantity')}</th>
                <th className="p-2 text-right">{t('reports.revenue')}</th>
              </tr>
            </thead>
            <tbody>
              {report.bestSellers.map((b) => (
                <tr key={b.productName} className="border-b border-neutral-200">
                  <td className="p-2">{b.productName}</td>
                  <td className="p-2 text-right">{b.quantity}</td>
                  <td className="p-2 text-right">{formatMoney(b.revenue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section>
          <h2 className="mb-2 text-2xl font-bold">{t('reports.times')}</h2>
          <dl className="grid grid-cols-2 gap-3">
            {[
              [t('reports.toAccept'), report.times.toAccept],
              [t('reports.acceptToReady'), report.times.acceptToReady],
              [t('reports.delivery'), report.times.delivery],
              [t('reports.placedToCompleted'), report.times.placedToCompleted],
            ].map(([label, value]) => (
              <div key={label as string} className="rounded-xl bg-white p-3 shadow-sm">
                <dt className="text-sm text-neutral-600">{label}</dt>
                <dd className="text-2xl font-bold">{minutes(value as number | null)}</dd>
              </div>
            ))}
          </dl>
        </section>
      </div>

      <section>
        <h2 className="mb-2 text-2xl font-bold">{t('reports.drivers')}</h2>
        <table className="w-full border-collapse bg-white text-left text-base">
          <thead>
            <tr className="border-b-2 border-neutral-300">
              <th className="p-2">{t('reports.driver')}</th>
              <th className="p-2 text-right">{t('reports.deliveries')}</th>
              <th className="p-2 text-right">{t('reports.avgDelivery')}</th>
              <th className="p-2 text-right">{t('reports.revenue')}</th>
            </tr>
          </thead>
          <tbody>
            {report.drivers.map((d) => (
              <tr key={d.driverId} className="border-b border-neutral-200">
                <td className="p-2">{d.driverName ?? '—'}</td>
                <td className="p-2 text-right">{d.deliveries}</td>
                <td className="p-2 text-right">{minutes(d.averageDeliveryMinutes)}</td>
                <td className="p-2 text-right">{formatMoney(d.total)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
