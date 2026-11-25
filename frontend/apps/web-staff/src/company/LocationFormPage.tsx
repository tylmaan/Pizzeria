import { errorCodeOf, type LocationDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, Checkbox, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useFormErrors } from '../lib/formErrors';
import { PageHeader, QueryState } from './PageHeader';
import { useCreateLocation, useLocation, useUpdateLocation } from './queries';

const listPath = '/company/locations';

export function NewLocationPage() {
  return <LocationForm />;
}

export function EditLocationPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const { id = '' } = useParams();
  const location = useLocation(id);

  if (location.isError) {
    return <Alert>{codes.error(errorCodeOf(location.error))}</Alert>;
  }
  if (location.data === undefined) {
    return <QueryState>{t('common.loading')}</QueryState>;
  }
  // Keyed by id so the form state is initialised from the loaded location exactly once.
  return <LocationForm key={location.data.id} existing={location.data} />;
}

function LocationForm({ existing }: { existing?: LocationDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const create = useCreateLocation();
  const update = useUpdateLocation(existing?.id ?? '');
  const mutation = existing ? update : create;
  const errors = useFormErrors(mutation.error);

  const [name, setName] = useState(existing?.name ?? '');
  const [street, setStreet] = useState(existing?.address.street ?? '');
  const [postalCode, setPostalCode] = useState(existing?.address.postalCode ?? '');
  const [city, setCity] = useState(existing?.address.city ?? '');
  const [isActive, setIsActive] = useState(existing?.isActive ?? true);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const address = { street, postalCode, city };
    const done = { onSuccess: () => void navigate(listPath) };
    if (existing) {
      update.mutate({ name, address, timeZoneId: existing.timeZoneId, isActive }, done);
    } else {
      create.mutate({ name, address, timeZoneId: null }, done);
    }
  };

  return (
    <section className="max-w-xl">
      <PageHeader title={existing ? t('locations.editTitle') : t('locations.newTitle')} />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <TextField
          label={t('locations.name')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={errors.field('name')}
          autoFocus
        />
        <TextField
          label={t('locations.street')}
          value={street}
          onChange={(e) => setStreet(e.target.value)}
          error={errors.field('address.street')}
        />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-[10rem_1fr]">
          <TextField
            label={t('locations.postalCode')}
            hint={t('locations.postalCodeHint')}
            inputMode="numeric"
            value={postalCode}
            onChange={(e) => setPostalCode(e.target.value)}
            error={errors.field('address.postalCode')}
          />
          <TextField
            label={t('locations.city')}
            value={city}
            onChange={(e) => setCity(e.target.value)}
            error={errors.field('address.city')}
          />
        </div>
        {existing && (
          <Checkbox label={t('locations.isActive')} checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        )}
        <div className="flex gap-3">
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
          <Link
            to={listPath}
            className="flex min-h-12 items-center rounded-lg bg-neutral-200 px-5 text-lg font-semibold hover:bg-neutral-300"
          >
            {t('common.cancel')}
          </Link>
        </div>
      </form>
    </section>
  );
}
