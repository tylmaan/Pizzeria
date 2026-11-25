import { errorCodeOf, type LocationDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { PageHeader, QueryState } from '../company/PageHeader';
import { useLocation } from '../company/queries';
import { useFormErrors } from '../lib/formErrors';
import { useDeliveryCheck, useZones } from './queries';
import { ZoneMap } from './ZoneMap';

export function ZonesPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const { locationId = '' } = useParams();
  const location = useLocation(locationId);
  const zones = useZones(locationId);
  const check = useDeliveryCheck();

  const error = location.error ?? zones.error;
  if (error) {
    return <Alert>{codes.error(errorCodeOf(error))}</Alert>;
  }
  if (location.data === undefined || zones.data === undefined) {
    return <QueryState>{t('common.loading')}</QueryState>;
  }

  return (
    <section className="flex flex-col gap-4">
      <PageHeader
        title={t('zones.title', { location: location.data.name })}
        action={{ to: `/company/locations/${locationId}/zones/new`, label: t('zones.add') }}
      />
      <ZoneMap
        label={t('zones.map')}
        zones={zones.data}
        marker={check.data?.point ?? null}
        fallbackCenter={location.data.position}
      />
      {zones.data.length === 0 && <QueryState>{t('zones.empty')}</QueryState>}
      <ul className="flex flex-col gap-2">
        {zones.data.map((zone) => (
          <li key={zone.id}>
            <Link
              to={`/company/locations/${locationId}/zones/${zone.id}`}
              className="flex min-h-16 flex-wrap items-center justify-between gap-2 rounded-lg bg-white px-4 py-3 shadow-sm hover:bg-orange-50"
            >
              <span className="text-xl font-semibold">
                {zone.name}
                {!zone.isActive && <span className="ml-2 text-base font-normal text-neutral-600">({t('common.inactive')})</span>}
              </span>
              <span className="text-base">
                {t('zones.summary', {
                  fee: formatMoney(zone.deliveryFee),
                  min: formatMoney(zone.minOrderValue),
                  minutes: zone.estimatedMinutes,
                })}
              </span>
            </Link>
          </li>
        ))}
      </ul>
      <AddressCheck location={location.data} check={check} />
    </section>
  );
}

function AddressCheck({ location, check }: { location: LocationDto; check: ReturnType<typeof useDeliveryCheck> }) {
  const { t } = useTranslation();
  const errors = useFormErrors(check.error);
  const [street, setStreet] = useState('');
  const [postalCode, setPostalCode] = useState(location.address.postalCode);
  const [city, setCity] = useState(location.address.city);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    check.mutate({ street, postalCode, city });
  };

  const quote = check.data;
  return (
    <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm" noValidate>
      <h2 className="text-xl font-bold">{t('zones.checkTitle')}</h2>
      {errors.general && <Alert>{errors.general}</Alert>}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[2fr_8rem_1fr]">
        <TextField label={t('locations.street')} value={street} onChange={(e) => setStreet(e.target.value)} error={errors.field('street')} />
        <TextField label={t('locations.postalCode')} value={postalCode} onChange={(e) => setPostalCode(e.target.value)} error={errors.field('postalCode')} />
        <TextField label={t('locations.city')} value={city} onChange={(e) => setCity(e.target.value)} error={errors.field('city')} />
      </div>
      <Button type="submit" className="self-start" disabled={check.isPending}>
        {t('zones.check')}
      </Button>
      {quote && (
        <p role="status" className={`text-lg font-semibold ${quote.delivers ? 'text-green-800' : 'text-red-800'}`}>
          {quote.delivers
            ? t('zones.delivers', {
                location: quote.locationName ?? '',
                zone: quote.zoneName ?? '',
                fee: formatMoney(quote.deliveryFee ?? 0),
                min: formatMoney(quote.minOrderValue ?? 0),
                minutes: quote.estimatedMinutes ?? 0,
              })
            : t('zones.noDelivery')}
        </p>
      )}
    </form>
  );
}
