import { errorCodeOf } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, buttonClassName } from '@pizzeria/ui';
import { Link } from 'react-router';
import { PageHeader, QueryState } from './PageHeader';
import { useLocations } from './queries';

export function LocationsPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const locations = useLocations();

  return (
    <section>
      <PageHeader title={t('locations.title')} action={{ to: '/company/locations/new', label: t('locations.add') }} />
      {locations.isPending && <QueryState>{t('common.loading')}</QueryState>}
      {locations.isError && <Alert>{codes.error(errorCodeOf(locations.error))}</Alert>}
      {locations.data?.length === 0 && <QueryState>{t('locations.empty')}</QueryState>}
      <ul className="flex flex-col gap-2">
        {locations.data?.map((location) => (
          <li key={location.id} className="flex flex-wrap items-center gap-2 rounded-lg bg-white pr-3 shadow-sm">
            <Link
              to={`/company/locations/${location.id}`}
              className="flex min-h-16 flex-1 flex-wrap items-center justify-between gap-2 rounded-lg px-4 py-3 hover:bg-orange-50"
            >
              <span>
                <span className="block text-xl font-semibold">{location.name}</span>
                <span className="block text-base text-neutral-700">
                  {location.address.street}, {location.address.postalCode} {location.address.city}
                </span>
              </span>
              <StatusBadge active={location.isActive} />
            </Link>
            <Link to={`/company/locations/${location.id}/zones`} className={buttonClassName('secondary')}>
              {t('zones.link')}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

function StatusBadge({ active }: { active: boolean }) {
  const { t } = useTranslation();
  return (
    <span
      className={`rounded-full px-3 py-1 text-base font-semibold ${
        active ? 'bg-green-100 text-green-800' : 'bg-neutral-200 text-neutral-700'
      }`}
    >
      {active ? t('common.active') : t('common.inactive')}
    </span>
  );
}
