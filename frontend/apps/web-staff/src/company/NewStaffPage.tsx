import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, Checkbox, SelectField, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { useFormErrors } from '../lib/formErrors';
import { PageHeader } from './PageHeader';
import { useAssignableRoles, useCreateStaff, useLocations } from './queries';

const listPath = '/company/staff';

export function NewStaffPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const navigate = useNavigate();
  const roles = useAssignableRoles();
  const locations = useLocations();
  const create = useCreateStaff();
  const errors = useFormErrors(create.error);

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('');
  const [locationIds, setLocationIds] = useState<string[]>([]);

  // Only location-scoped roles (manager, staff, kitchen, driver) are tied to locations.
  const requiresLocations = roles.data?.find((r) => r.role === role)?.requiresLocations ?? false;

  const toggleLocation = (id: string, checked: boolean) =>
    setLocationIds((current) => (checked ? [...current, id] : current.filter((x) => x !== id)));

  const submit = (event: FormEvent) => {
    event.preventDefault();
    create.mutate(
      { displayName, email, password, role, locationIds: requiresLocations ? locationIds : null },
      { onSuccess: () => void navigate(listPath) },
    );
  };

  return (
    <section className="max-w-xl">
      <PageHeader title={t('staff.newTitle')} />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <TextField
          label={t('staff.displayName')}
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          error={errors.field('displayName')}
          autoFocus
        />
        <TextField
          label={t('staff.email')}
          type="email"
          autoComplete="off"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={errors.field('email')}
        />
        <TextField
          label={t('staff.password')}
          hint={t('staff.passwordHint')}
          type="text"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={errors.field('password')}
        />
        <SelectField
          label={t('staff.role')}
          value={role}
          onChange={(e) => setRole(e.target.value)}
          error={errors.field('role')}
        >
          <option value="" disabled>
            —
          </option>
          {roles.data?.map((r) => (
            <option key={r.role} value={r.role}>
              {codes.role(r.role)}
            </option>
          ))}
        </SelectField>
        {requiresLocations && (
          <fieldset className="flex flex-col gap-1">
            <legend className="text-base font-semibold">{t('staff.locations')}</legend>
            <p className="text-sm text-neutral-600">{t('staff.locationsHint')}</p>
            {locations.data?.map((location) => (
              <Checkbox
                key={location.id}
                label={location.name}
                checked={locationIds.includes(location.id)}
                onChange={(e) => toggleLocation(location.id, e.target.checked)}
              />
            ))}
          </fieldset>
        )}
        <div className="flex gap-3">
          <Button type="submit" disabled={create.isPending || role === ''}>
            {create.isPending ? t('common.saving') : t('common.save')}
          </Button>
          <Link
            to={listPath}
            className={buttonClassName('secondary')}
          >
            {t('common.cancel')}
          </Link>
        </div>
      </form>
    </section>
  );
}
