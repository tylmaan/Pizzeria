import { type CategoryDto, type ModifierGroupDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, Checkbox, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { PageHeader } from '../company/PageHeader';
import { useFormErrors } from '../lib/formErrors';
import { MenuEntityLoader } from './MenuEntityLoader';
import { toPriceInputs, toPriceList, type PriceInputs } from './prices';
import { useSaveModifierGroup } from './queries';

const menuPath = '/company/menu';

export function NewModifierGroupPage() {
  const categoryId = useSearchParams()[0].get('category') ?? '';
  return (
    <MenuEntityLoader find={(menu) => menu.categories.find((c) => c.id === categoryId)}>
      {(category) => <ModifierGroupForm category={category} />}
    </MenuEntityLoader>
  );
}

export function EditModifierGroupPage() {
  const { id = '' } = useParams();
  return (
    <MenuEntityLoader find={(menu) => menu.modifierGroups.find((g) => g.id === id)}>
      {(group, menu) => {
        const category = menu.categories.find((c) => c.id === group.categoryId);
        return category && <ModifierGroupForm key={group.id} category={category} existing={group} />;
      }}
    </MenuEntityLoader>
  );
}

type ModifierRow = { key: string; id: string | null; name: string; isActive: boolean; prices: PriceInputs };

let nextKey = 0;
const newRow = (row: Omit<ModifierRow, 'key'> = { id: null, name: '', isActive: true, prices: {} }): ModifierRow => ({
  key: `modifier-${nextKey++}`,
  ...row,
});

function ModifierGroupForm({ category, existing }: { category: CategoryDto; existing?: ModifierGroupDto }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const navigate = useNavigate();
  const save = useSaveModifierGroup(existing?.id);
  const errors = useFormErrors(save.error);
  const sizeIds = category.sizes.map((s) => s.id);

  const [name, setName] = useState(existing?.name ?? '');
  const [minSelect, setMinSelect] = useState(String(existing?.minSelect ?? 0));
  const [maxSelect, setMaxSelect] = useState(String(existing?.maxSelect ?? 1));
  const [rows, setRows] = useState<ModifierRow[]>(
    existing
      ? existing.modifiers.map((m) => newRow({ id: m.id, name: m.name, isActive: m.isActive, prices: toPriceInputs(m.prices) }))
      : [newRow()],
  );
  const [invalid, setInvalid] = useState<Record<string, string[]>>({});
  const [sent, setSent] = useState<string[][]>([]);

  const updateRow = (key: string, change: Partial<ModifierRow>) =>
    setRows((current) => current.map((row) => (row.key === key ? { ...row, ...change } : row)));

  const priceError = (row: ModifierRow, index: number, sizeId: string): string | undefined => {
    if (invalid[row.key]?.includes(sizeId)) {
      return codes.validation('validation.price');
    }
    const sentIndex = sent[index]?.indexOf(sizeId) ?? -1;
    return sentIndex >= 0 ? errors.field(`modifiers[${index}].prices[${sentIndex}].price`) : undefined;
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const lists = rows.map((row) => toPriceList(row.prices, sizeIds));
    const invalidByRow = Object.fromEntries(rows.map((row, i) => [row.key, lists[i]!.invalidSizeIds]));
    setInvalid(invalidByRow);
    if (lists.some((l) => l.invalidSizeIds.length > 0)) {
      return;
    }
    setSent(lists.map((l) => l.prices.map((p) => p.sizeId)));
    save.mutate(
      {
        categoryId: category.id,
        name,
        minSelect: Number(minSelect),
        maxSelect: Number(maxSelect),
        modifiers: rows.map((row, i) => ({ id: row.id, name: row.name, isActive: row.isActive, prices: lists[i]!.prices })),
      },
      { onSuccess: () => void navigate(menuPath) },
    );
  };

  return (
    <section className="max-w-3xl">
      <PageHeader title={existing ? t('menu.editModifierGroupTitle') : t('menu.newModifierGroupTitle')} />
      <p className="mb-4 text-lg">
        {t('menu.category')}: <strong>{category.name}</strong>
      </p>
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <TextField
          label={t('menu.groupName')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={errors.field('name')}
          autoFocus
        />
        <div className="grid grid-cols-2 gap-3">
          <TextField
            label={t('menu.minSelect')}
            hint={t('menu.minSelectHint')}
            type="number"
            min={0}
            value={minSelect}
            onChange={(e) => setMinSelect(e.target.value)}
            error={errors.field('minSelect')}
          />
          <TextField
            label={t('menu.maxSelect')}
            type="number"
            min={1}
            value={maxSelect}
            onChange={(e) => setMaxSelect(e.target.value)}
            error={errors.field('maxSelect')}
          />
        </div>
        <fieldset className="flex flex-col gap-3">
          <legend className="text-base font-semibold">{t('menu.modifiers')}</legend>
          {rows.map((row, index) => (
            <div key={row.key} className="flex flex-col gap-3 rounded-lg border-2 border-neutral-300 bg-white p-3">
              <TextField
                label={t('menu.modifierName')}
                value={row.name}
                onChange={(e) => updateRow(row.key, { name: e.target.value })}
                error={errors.field(`modifiers[${index}].name`)}
              />
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                {category.sizes.map((size) => (
                  <TextField
                    key={size.id}
                    label={size.name}
                    inputMode="decimal"
                    value={row.prices[size.id] ?? ''}
                    onChange={(e) => updateRow(row.key, { prices: { ...row.prices, [size.id]: e.target.value } })}
                    error={priceError(row, index, size.id)}
                  />
                ))}
              </div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Checkbox
                  label={t('menu.modifierActive')}
                  checked={row.isActive}
                  onChange={(e) => updateRow(row.key, { isActive: e.target.checked })}
                />
                <Button
                  variant="secondary"
                  disabled={rows.length === 1}
                  onClick={() => setRows((current) => current.filter((r) => r.key !== row.key))}
                >
                  {t('common.remove')}
                </Button>
              </div>
            </div>
          ))}
          <Button variant="secondary" className="self-start" onClick={() => setRows((current) => [...current, newRow()])}>
            {t('menu.addModifier')}
          </Button>
        </fieldset>
        <div className="flex gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
          <Link to={menuPath} className={buttonClassName('secondary')}>
            {t('common.cancel')}
          </Link>
        </div>
      </form>
    </section>
  );
}
