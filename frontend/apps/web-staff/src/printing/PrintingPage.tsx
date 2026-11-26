import { errorCodeOf, Roles, type FiscalReceiptTrigger, type PrintAgentDto, type PrintSettingsDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, Checkbox, SelectField, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { hasAnyRole, useCurrentUser } from '../auth/session';
import { useLocations } from '../company/queries';
import { useFormErrors } from '../lib/formErrors';
import {
  usePrintAgents,
  usePrintJobs,
  usePrintSettings,
  useRegisterAgent,
  useRetryPrintJob,
  useRevokeAgent,
  useSavePrintSettings,
} from './queries';

const time = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('pl-PL', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : '—';

const triggers: FiscalReceiptTrigger[] = ['Never', 'OnAccepted', 'OnCompleted'];

/** Local agents (online, printers), print settings and recent printouts of a location. */
export function PrintingPage() {
  const { t } = useTranslation();
  const locations = useLocations();
  const [picked, setPicked] = useState('');
  const locationId = picked || (locations.data?.[0]?.id ?? '');
  const canManage = hasAnyRole(useCurrentUser(), [Roles.CompanyAdmin, Roles.LocationManager]);

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-3xl font-bold">{t('printing.title')}</h1>
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
      {locationId !== '' && (
        <>
          <Agents locationId={locationId} canManage={canManage} />
          {canManage && <Settings key={locationId} locationId={locationId} />}
          <Jobs locationId={locationId} />
        </>
      )}
    </section>
  );
}

function Agents({ locationId, canManage }: { locationId: string; canManage: boolean }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const agents = usePrintAgents(locationId);
  const revoke = useRevokeAgent(locationId);
  const active = agents.data?.filter((a) => !a.isRevoked) ?? [];

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-2xl font-bold">{t('printing.agents')}</h2>
      {agents.error && <Alert>{codes.error(errorCodeOf(agents.error))}</Alert>}
      {revoke.error && <Alert>{codes.error(errorCodeOf(revoke.error))}</Alert>}
      {agents.data && active.length === 0 && <Alert tone="info">{t('printing.noAgents')}</Alert>}
      <ul className="flex flex-col gap-2">
        {active.map((agent) => (
          <li key={agent.id} aria-label={agent.name} className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-white px-4 py-3 shadow-sm">
            <div className="flex flex-col gap-1">
              <span className="text-lg font-semibold">
                {agent.name}{' '}
                <span className={agent.isOnline ? 'text-green-800' : 'text-red-800'}>
                  {agent.isOnline ? t('printing.online') : t('printing.offline')}
                </span>
              </span>
              <span className="text-base text-neutral-700">
                {t('printing.receiptPrinter')}: {printerLabel(agent, agent.receiptPrinterOnline, t)} · {t('printing.fiscalPrinter')}:{' '}
                {printerLabel(agent, agent.fiscalPrinterOnline, t)} · {t('printing.lastSeen', { time: time(agent.lastSeenAt) })}
              </span>
            </div>
            {canManage && (
              <Button variant="secondary" disabled={revoke.isPending} onClick={() => revoke.mutate(agent.id)}>
                {t('printing.revoke')}
              </Button>
            )}
          </li>
        ))}
      </ul>
      {canManage && <RegisterAgent locationId={locationId} />}
    </div>
  );
}

function printerLabel(agent: PrintAgentDto, online: boolean | null, t: ReturnType<typeof useTranslation>['t']) {
  if (!agent.isOnline) {
    return t('printing.unknown');
  }
  if (online === null) {
    return t('printing.notConfigured');
  }
  return online ? t('printing.online') : t('printing.offline');
}

function RegisterAgent({ locationId }: { locationId: string }) {
  const { t } = useTranslation();
  const register = useRegisterAgent(locationId);
  const errors = useFormErrors(register.error);
  const [name, setName] = useState('');

  return (
    <div className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm">
      <h3 className="text-xl font-semibold">{t('printing.addAgent')}</h3>
      {errors.general && <Alert>{errors.general}</Alert>}
      <form
        className="grid grid-cols-1 gap-3 md:grid-cols-[1fr_auto] md:items-end"
        onSubmit={(e) => {
          e.preventDefault();
          register.mutate(name.trim(), { onSuccess: () => setName('') });
        }}
      >
        <TextField label={t('printing.agentName')} value={name} onChange={(e) => setName(e.target.value)} error={errors.field('name')} />
        <Button type="submit" disabled={name.trim() === '' || register.isPending}>
          {t('printing.register')}
        </Button>
      </form>
      {register.data && (
        <Alert tone="success">
          <p className="font-semibold">{t('printing.tokenOnce', { name: register.data.agent.name })}</p>
          <code className="mt-2 block break-all rounded bg-white px-3 py-2 font-mono text-base select-all">{register.data.token}</code>
          <p className="mt-2">{t('printing.tokenHowTo')}</p>
        </Alert>
      )}
    </div>
  );
}

function Settings({ locationId }: { locationId: string }) {
  const settings = usePrintSettings(locationId);
  return settings.data ? <SettingsForm locationId={locationId} initial={settings.data} /> : null;
}

function SettingsForm({ locationId, initial }: { locationId: string; initial: PrintSettingsDto }) {
  const { t } = useTranslation();
  const save = useSavePrintSettings(locationId);
  const errors = useFormErrors(save.error);
  const [kitchen, setKitchen] = useState(initial.printKitchenTickets);
  const [fiscal, setFiscal] = useState<FiscalReceiptTrigger>(initial.fiscalReceiptTrigger);

  return (
    <form
      className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate({ printKitchenTickets: kitchen, fiscalReceiptTrigger: fiscal });
      }}
    >
      <h2 className="text-2xl font-bold">{t('printing.settings')}</h2>
      {errors.general && <Alert>{errors.general}</Alert>}
      {save.isSuccess && <Alert tone="success">{t('printing.saved')}</Alert>}
      <Checkbox label={t('printing.kitchenTickets')} checked={kitchen} onChange={(e) => setKitchen(e.target.checked)} />
      <SelectField label={t('printing.fiscalTrigger')} value={fiscal} onChange={(e) => setFiscal(e.target.value as FiscalReceiptTrigger)}>
        {triggers.map((trigger) => (
          <option key={trigger} value={trigger}>
            {t(`printing.trigger.${trigger}`)}
          </option>
        ))}
      </SelectField>
      <p className="text-base text-neutral-700">{t('printing.fiscalHint')}</p>
      <div>
        <Button type="submit" disabled={save.isPending}>
          {t('printing.save')}
        </Button>
      </div>
    </form>
  );
}

function Jobs({ locationId }: { locationId: string }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const jobs = usePrintJobs(locationId);
  const retry = useRetryPrintJob(locationId);

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-2xl font-bold">{t('printing.jobs')}</h2>
      {jobs.error && <Alert>{codes.error(errorCodeOf(jobs.error))}</Alert>}
      {retry.error && <Alert>{codes.error(errorCodeOf(retry.error))}</Alert>}
      {jobs.data?.length === 0 && <p className="text-lg">{t('printing.noJobs')}</p>}
      {(jobs.data?.length ?? 0) > 0 && (
        <table className="w-full border-collapse bg-white text-left text-lg shadow-sm">
          <thead>
            <tr className="border-b text-base text-neutral-600">
              <th className="px-3 py-2">{t('printing.when')}</th>
              <th className="px-3 py-2">{t('printing.kind')}</th>
              <th className="px-3 py-2">{t('printing.status')}</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {jobs.data?.map((job) => (
              <tr key={job.id} className="border-b last:border-0">
                <td className="px-3 py-2">{time(job.createdAt)}</td>
                <td className="px-3 py-2">{t(`printing.kinds.${job.kind}`)}</td>
                <td className={`px-3 py-2 ${job.status === 'Failed' ? 'font-semibold text-red-800' : ''}`}>
                  {t(`printing.statuses.${job.status}`)}
                  {job.error && ` — ${job.error}`}
                </td>
                <td className="px-3 py-2 text-right">
                  {job.status === 'Failed' && (
                    <Button variant="secondary" disabled={retry.isPending} onClick={() => retry.mutate(job.id)}>
                      {t('printing.retry')}
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
