import { errorCodeOf, type DeliveryZoneDto, type GeoPointDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, Checkbox, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { PageHeader, QueryState } from '../company/PageHeader';
import { useLocation } from '../company/queries';
import { useFormErrors } from '../lib/formErrors';
import { parsePrice, priceInputValue } from '../menu/prices';
import { useSaveZone, useZones } from './queries';
import { ZoneMap } from './ZoneMap';

export function ZoneFormPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const { locationId = '', zoneId } = useParams();
  const zones = useZones(locationId);
  const location = useLocation(locationId);

  const error = zones.error ?? location.error;
  if (error) {
    return <Alert>{codes.error(errorCodeOf(error))}</Alert>;
  }
  if (zones.data === undefined || location.data === undefined) {
    return <QueryState>{t('common.loading')}</QueryState>;
  }
  const existing = zoneId ? zones.data.find((z) => z.id === zoneId) : undefined;
  if (zoneId && !existing) {
    return <Alert>{codes.error('delivery.zone_not_found')}</Alert>;
  }
  return (
    <ZoneForm
      key={existing?.id ?? 'new'}
      locationId={locationId}
      existing={existing}
      otherZones={zones.data.filter((z) => z.id !== zoneId)}
      center={location.data.position}
    />
  );
}

function ZoneForm({
  locationId,
  existing,
  otherZones,
  center,
}: {
  locationId: string;
  existing: DeliveryZoneDto | undefined;
  otherZones: DeliveryZoneDto[];
  center: GeoPointDto | null;
}) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const navigate = useNavigate();
  const save = useSaveZone(locationId, existing?.id);
  const errors = useFormErrors(save.error);
  const listPath = `/company/locations/${locationId}/zones`;

  const [name, setName] = useState(existing?.name ?? '');
  const [fee, setFee] = useState(priceInputValue(existing?.deliveryFee));
  const [minOrder, setMinOrder] = useState(priceInputValue(existing?.minOrderValue));
  const [minutes, setMinutes] = useState(String(existing?.estimatedMinutes ?? 45));
  const [isActive, setIsActive] = useState(existing?.isActive ?? true);
  const [boundary, setBoundary] = useState<GeoPointDto[]>(existing?.boundary ?? []);
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({});

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const deliveryFee = parsePrice(fee);
    const minOrderValue = parsePrice(minOrder);
    const found: Record<string, string> = {};
    if (deliveryFee === null || Number.isNaN(deliveryFee)) {
      found.deliveryFee = codes.validation('validation.price');
    }
    if (minOrderValue === null || Number.isNaN(minOrderValue)) {
      found.minOrderValue = codes.validation('validation.price');
    }
    if (boundary.length < 3) {
      found.boundary = t('zones.boundaryRequired');
    }
    setLocalErrors(found);
    if (Object.keys(found).length > 0) {
      return;
    }
    save.mutate(
      {
        name,
        boundary,
        deliveryFee: deliveryFee!,
        minOrderValue: minOrderValue!,
        estimatedMinutes: Number(minutes),
        isActive,
      },
      { onSuccess: () => void navigate(listPath) },
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <PageHeader title={existing ? t('zones.editTitle') : t('zones.newTitle')} />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-[2fr_1fr_1fr_1fr]">
          <TextField label={t('zones.name')} value={name} onChange={(e) => setName(e.target.value)} error={errors.field('name')} autoFocus />
          <TextField
            label={t('zones.deliveryFee')}
            inputMode="decimal"
            value={fee}
            onChange={(e) => setFee(e.target.value)}
            error={localErrors.deliveryFee ?? errors.field('deliveryFee')}
          />
          <TextField
            label={t('zones.minOrderValue')}
            inputMode="decimal"
            value={minOrder}
            onChange={(e) => setMinOrder(e.target.value)}
            error={localErrors.minOrderValue ?? errors.field('minOrderValue')}
          />
          <TextField
            label={t('zones.estimatedMinutes')}
            type="number"
            min={1}
            value={minutes}
            onChange={(e) => setMinutes(e.target.value)}
            error={errors.field('estimatedMinutes')}
          />
        </div>
        <p className="text-base">{t('zones.drawHint')}</p>
        <ZoneMap
          label={t('zones.map')}
          zones={otherZones}
          boundary={boundary}
          onBoundaryChange={setBoundary}
          fallbackCenter={center}
        />
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-base font-semibold">{t('zones.points', { count: boundary.length })}</span>
          <Button variant="secondary" disabled={boundary.length === 0} onClick={() => setBoundary((b) => b.slice(0, -1))}>
            {t('zones.undoPoint')}
          </Button>
          <Button variant="secondary" disabled={boundary.length === 0} onClick={() => setBoundary([])}>
            {t('zones.clear')}
          </Button>
        </div>
        {(localErrors.boundary ?? errors.field('boundary')) && <Alert>{localErrors.boundary ?? errors.field('boundary')}</Alert>}
        {existing && <Checkbox label={t('zones.isActive')} checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />}
        <div className="flex gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
          <Link to={listPath} className={buttonClassName('secondary')}>
            {t('common.cancel')}
          </Link>
        </div>
      </form>
    </section>
  );
}
