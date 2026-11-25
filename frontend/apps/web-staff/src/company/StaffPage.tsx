import { errorCodeOf } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import { PageHeader, QueryState } from './PageHeader';
import { useLocations, useStaff } from './queries';

export function StaffPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const staff = useStaff();
  const locations = useLocations();
  const locationName = new Map(locations.data?.map((l) => [l.id, l.name]));

  return (
    <section>
      <PageHeader title={t('staff.title')} action={{ to: '/company/staff/new', label: t('staff.add') }} />
      {staff.isPending && <QueryState>{t('common.loading')}</QueryState>}
      {staff.isError && <Alert>{codes.error(errorCodeOf(staff.error))}</Alert>}
      {staff.data?.length === 0 && <QueryState>{t('staff.empty')}</QueryState>}
      <ul className="flex flex-col gap-2">
        {staff.data?.map((user) => (
          <li
            key={user.id}
            className="flex min-h-16 flex-wrap items-center justify-between gap-2 rounded-lg bg-white px-4 py-3 shadow-sm"
          >
            <span>
              <span className="block text-xl font-semibold">{user.displayName}</span>
              <span className="block text-base text-neutral-700">{user.email}</span>
            </span>
            <span className="text-right">
              <span className="block text-lg font-semibold">{codes.role(user.role)}</span>
              <span className="block text-base text-neutral-700">
                {user.locationIds.length === 0
                  ? t('staff.allLocations')
                  : user.locationIds.map((id) => locationName.get(id) ?? '…').join(', ')}
              </span>
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
