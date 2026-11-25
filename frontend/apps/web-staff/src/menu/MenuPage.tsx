import { errorCodeOf, type CategoryDto, type HalfAndHalfPricing, type MenuDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, buttonClassName, SelectField } from '@pizzeria/ui';
import { Link } from 'react-router';
import { PageHeader, QueryState } from '../company/PageHeader';
import { useMenu, useSaveMenuSettings } from './queries';

export function MenuPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const menu = useMenu();
  const data = menu.data;

  return (
    <section>
      <PageHeader title={t('menu.title')} action={{ to: '/company/menu/categories/new', label: t('menu.addCategory') }} />
      {menu.isError && <Alert>{codes.error(errorCodeOf(menu.error))}</Alert>}
      {data === undefined ? (
        !menu.isError && <QueryState>{t('common.loading')}</QueryState>
      ) : (
        <div className="flex flex-col gap-6">
          <HalfAndHalfSetting current={data.halfAndHalfPricing} />
          {data.categories.length === 0 && <QueryState>{t('menu.empty')}</QueryState>}
          {data.categories.map((category) => (
            <CategorySection key={category.id} category={category} menu={data} />
          ))}
        </div>
      )}
    </section>
  );
}

function HalfAndHalfSetting({ current }: { current: HalfAndHalfPricing }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const save = useSaveMenuSettings();
  const options: HalfAndHalfPricing[] = ['MoreExpensiveHalf', 'AverageOfHalves'];

  return (
    <div className="flex max-w-md flex-col gap-2 rounded-lg bg-white p-4 shadow-sm">
      <SelectField
        label={t('menu.halfAndHalfPricing')}
        value={save.variables ?? current}
        disabled={save.isPending}
        onChange={(e) => save.mutate(e.target.value as HalfAndHalfPricing)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {t(`menu.halfAndHalf.${option}`)}
          </option>
        ))}
      </SelectField>
      {save.isError && <Alert>{codes.error(errorCodeOf(save.error))}</Alert>}
      {save.isSuccess && <p className="text-base text-green-800">{t('common.saved')}</p>}
    </div>
  );
}

function CategorySection({ category, menu }: { category: CategoryDto; menu: MenuDto }) {
  const { t } = useTranslation();
  const products = menu.products.filter((p) => p.categoryId === category.id);
  const groups = menu.modifierGroups.filter((g) => g.categoryId === category.id);
  const sizeName = new Map(category.sizes.map((s) => [s.id, s.name]));

  return (
    <article className="rounded-xl bg-white p-4 shadow-sm">
      <header className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-bold">{category.name}</h2>
          <p className="text-base text-neutral-700">{category.sizes.map((s) => s.name).join(' · ')}</p>
        </div>
        <Link to={`/company/menu/categories/${category.id}`} className={buttonClassName('secondary')}>
          {t('menu.editCategory')}
        </Link>
      </header>

      <h3 className="mb-2 text-lg font-semibold">{t('menu.products')}</h3>
      {products.length === 0 && <p className="mb-2 text-base text-neutral-700">{t('menu.emptyCategory')}</p>}
      <ul className="mb-3 flex flex-col gap-1">
        {products.map((product) => (
          <li key={product.id}>
            <Link
              to={`/company/menu/products/${product.id}`}
              className="flex min-h-12 flex-wrap items-center justify-between gap-2 rounded-lg px-3 py-2 hover:bg-orange-50"
            >
              <span className="text-lg font-semibold">
                {product.name}
                {product.allowsHalves && <Badge>{t('menu.halves')}</Badge>}
                {!product.isActive && <Badge muted>{t('menu.inactive')}</Badge>}
              </span>
              <span className="text-base">
                {category.sizes
                  .filter((s) => product.prices.some((p) => p.sizeId === s.id))
                  .map((s) => `${sizeName.get(s.id)}: ${formatMoney(product.prices.find((p) => p.sizeId === s.id)!.price)}`)
                  .join(' · ')}
              </span>
            </Link>
          </li>
        ))}
      </ul>
      <Link to={`/company/menu/products/new?category=${category.id}`} className={buttonClassName('secondary')}>
        {t('menu.addProduct')}
      </Link>

      <h3 className="mb-2 mt-5 text-lg font-semibold">{t('menu.modifierGroups')}</h3>
      {groups.length === 0 && <p className="mb-2 text-base text-neutral-700">{t('menu.noModifierGroups')}</p>}
      <ul className="mb-3 flex flex-col gap-1">
        {groups.map((group) => (
          <li key={group.id}>
            <Link
              to={`/company/menu/modifier-groups/${group.id}`}
              className="flex min-h-12 flex-wrap items-center justify-between gap-2 rounded-lg px-3 py-2 hover:bg-orange-50"
            >
              <span className="text-lg font-semibold">
                {group.name}{' '}
                <span className="text-base font-normal text-neutral-700">
                  ({t('menu.selection', { min: group.minSelect, max: group.maxSelect })})
                </span>
              </span>
              <span className="text-base">{group.modifiers.map((m) => m.name).join(', ')}</span>
            </Link>
          </li>
        ))}
      </ul>
      <Link to={`/company/menu/modifier-groups/new?category=${category.id}`} className={buttonClassName('secondary')}>
        {t('menu.addModifierGroup')}
      </Link>
    </article>
  );
}

function Badge({ children, muted = false }: { children: string; muted?: boolean }) {
  return (
    <span
      className={`ml-2 rounded-full px-2 py-0.5 text-sm font-semibold ${
        muted ? 'bg-neutral-200 text-neutral-700' : 'bg-orange-100 text-orange-800'
      }`}
    >
      {children}
    </span>
  );
}
