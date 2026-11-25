import { type MenuDto, type ProductDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, Checkbox, SelectField, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { PageHeader } from '../company/PageHeader';
import { useFormErrors } from '../lib/formErrors';
import { MenuEntityLoader } from './MenuEntityLoader';
import { toPriceInputs, toPriceList, type PriceInputs } from './prices';
import { useSaveProduct } from './queries';

const menuPath = '/company/menu';

export function NewProductPage() {
  const categoryId = useSearchParams()[0].get('category') ?? '';
  return (
    <MenuEntityLoader find={(menu) => menu}>{(menu) => <ProductForm menu={menu} initialCategoryId={categoryId} />}</MenuEntityLoader>
  );
}

export function EditProductPage() {
  const { id = '' } = useParams();
  return (
    <MenuEntityLoader find={(menu) => menu.products.find((p) => p.id === id)}>
      {(product, menu) => (
        <ProductForm key={product.id} menu={menu} existing={product} initialCategoryId={product.categoryId} />
      )}
    </MenuEntityLoader>
  );
}

function ProductForm({
  menu,
  existing,
  initialCategoryId,
}: {
  menu: MenuDto;
  existing?: ProductDto;
  initialCategoryId: string;
}) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const navigate = useNavigate();
  const save = useSaveProduct(existing?.id);
  const errors = useFormErrors(save.error);

  const [categoryId, setCategoryId] = useState(initialCategoryId || (menu.categories[0]?.id ?? ''));
  const [name, setName] = useState(existing?.name ?? '');
  const [description, setDescription] = useState(existing?.description ?? '');
  const [allowsHalves, setAllowsHalves] = useState(existing?.allowsHalves ?? false);
  const [isActive, setIsActive] = useState(existing?.isActive ?? true);
  const [prices, setPrices] = useState<PriceInputs>(existing ? toPriceInputs(existing.prices) : {});
  const [groupIds, setGroupIds] = useState<string[]>(existing?.modifierGroupIds ?? []);
  const [invalidSizeIds, setInvalidSizeIds] = useState<string[]>([]);
  // Size ids in the order they were sent: API errors point at `prices[i]` of the request.
  const [sentSizeIds, setSentSizeIds] = useState<string[]>([]);

  const category = menu.categories.find((c) => c.id === categoryId);
  const groups = menu.modifierGroups.filter((g) => g.categoryId === categoryId);

  const changeCategory = (id: string) => {
    // Prices and modifier groups belong to the category's sizes, so they do not carry over.
    setCategoryId(id);
    setPrices({});
    setGroupIds([]);
  };

  const priceError = (sizeId: string): string | undefined => {
    if (invalidSizeIds.includes(sizeId)) {
      return codes.validation('validation.price');
    }
    const index = sentSizeIds.indexOf(sizeId);
    return index >= 0 ? errors.field(`prices[${index}].price`) : undefined;
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const list = toPriceList(prices, category?.sizes.map((s) => s.id) ?? []);
    setInvalidSizeIds(list.invalidSizeIds);
    if (list.invalidSizeIds.length > 0) {
      return;
    }
    setSentSizeIds(list.prices.map((p) => p.sizeId));
    save.mutate(
      {
        categoryId,
        name,
        description: description.trim() === '' ? null : description,
        allowsHalves,
        isActive,
        prices: list.prices,
        modifierGroupIds: groupIds,
      },
      { onSuccess: () => void navigate(menuPath) },
    );
  };

  return (
    <section className="max-w-xl">
      <PageHeader title={existing ? t('menu.editProductTitle') : t('menu.newProductTitle')} />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <SelectField label={t('menu.category')} value={categoryId} onChange={(e) => changeCategory(e.target.value)}>
          {menu.categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </SelectField>
        <TextField
          label={t('menu.productName')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={errors.field('name')}
          autoFocus
        />
        <TextField
          label={t('menu.description')}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          error={errors.field('description')}
        />
        <fieldset className="flex flex-col gap-3">
          <legend className="text-base font-semibold">{t('menu.prices')}</legend>
          <p className="text-sm text-neutral-600">{t('menu.pricesHint')}</p>
          {errors.field('prices') && <Alert>{errors.field('prices')}</Alert>}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {category?.sizes.map((size) => (
              <TextField
                key={size.id}
                label={size.name}
                inputMode="decimal"
                value={prices[size.id] ?? ''}
                onChange={(e) => setPrices((current) => ({ ...current, [size.id]: e.target.value }))}
                error={priceError(size.id)}
              />
            ))}
          </div>
        </fieldset>
        <Checkbox label={t('menu.allowsHalves')} checked={allowsHalves} onChange={(e) => setAllowsHalves(e.target.checked)} />
        {existing && (
          <Checkbox label={t('menu.productActive')} checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        )}
        <fieldset className="flex flex-col gap-1">
          <legend className="text-base font-semibold">{t('menu.modifierGroups')}</legend>
          {groups.length === 0 && <p className="text-sm text-neutral-600">{t('menu.noModifierGroups')}</p>}
          {groups.map((group) => (
            <Checkbox
              key={group.id}
              label={group.name}
              checked={groupIds.includes(group.id)}
              onChange={(e) =>
                setGroupIds((ids) => (e.target.checked ? [...ids, group.id] : ids.filter((id) => id !== group.id)))
              }
            />
          ))}
        </fieldset>
        <div className="flex gap-3">
          <Button type="submit" disabled={save.isPending || !category}>
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
