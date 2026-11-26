import type { MenuDto, ModifierGroupDto, ProductDto } from '@pizzeria/api-client';
import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Button, Checkbox, SelectField } from '@pizzeria/ui';
import { useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router';
import { useShop } from '../app/services';
import { useCart } from '../cart/cart';
import { allowedGroups, previewUnitPrice, priceAt, selectionIsComplete } from '../menu/pricing';

export function ProductPage() {
  const { productId = '' } = useParams();
  const menu = useShop().data!.menu;
  const product = menu.products.find((p) => p.id === productId);
  return product ? <ProductForm key={product.id} menu={menu} product={product} /> : <Navigate to="/" replace />;
}

function ProductForm({ menu, product }: { menu: MenuDto; product: ProductDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const cart = useCart();
  const category = menu.categories.find((c) => c.id === product.categoryId)!;
  const sizes = category.sizes.filter((s) => priceAt(product, s.id) !== null);

  const [sizeId, setSizeId] = useState(sizes[0]?.id ?? '');
  const [halves, setHalves] = useState(false);
  const [secondHalfId, setSecondHalfId] = useState('');
  const [modifierIds, setModifierIds] = useState<string[]>([]);
  const [quantity, setQuantity] = useState(1);

  const halfCandidates = menu.products.filter(
    (p) => p.id !== product.id && p.allowsHalves && p.categoryId === product.categoryId && priceAt(p, sizeId) !== null,
  );
  const secondHalf = halves ? (menu.products.find((p) => p.id === secondHalfId) ?? null) : null;
  const groups = allowedGroups(menu, product, secondHalf);
  // Drop choices that are no longer offered after changing the second half.
  const validModifierIds = modifierIds.filter((id) => groups.some((g) => g.modifiers.some((m) => m.id === id)));

  const choice = { productId: product.id, secondHalfProductId: secondHalf?.id ?? null, sizeId, modifierIds: validModifierIds };
  const unitPrice = previewUnitPrice(menu, choice);
  const canAdd = unitPrice !== null && (!halves || secondHalf !== null) && selectionIsComplete(groups, validModifierIds);

  const toggle = (group: ModifierGroupDto, modifierId: string, checked: boolean) => {
    const groupIds = group.modifiers.map((m) => m.id);
    setModifierIds((current) => {
      const others = current.filter((id) => !groupIds.includes(id));
      const inGroup = current.filter((id) => groupIds.includes(id));
      // Single choice groups behave like radio buttons.
      if (group.maxSelect === 1) {
        return checked ? [...others, modifierId] : others;
      }
      return checked ? [...others, ...inGroup, modifierId] : [...others, ...inGroup.filter((id) => id !== modifierId)];
    });
  };

  const add = () => {
    cart.add(choice, quantity);
    void navigate('/');
  };

  return (
    <div className="flex flex-col gap-5">
      <Link to="/" className="text-lg font-semibold text-orange-700">
        ← {t('shop.backToMenu')}
      </Link>
      <div>
        <h1 className="text-3xl font-bold">{product.name}</h1>
        {product.description && <p className="text-lg text-neutral-700">{product.description}</p>}
      </div>

      <fieldset className="flex flex-col gap-1">
        <legend className="mb-1 text-lg font-semibold">{t('shop.size')}</legend>
        {sizes.map((size) => (
          <label key={size.id} className="flex min-h-12 items-center justify-between gap-3 rounded-lg bg-white px-3 text-lg">
            <span className="flex items-center gap-3">
              <input type="radio" name="size" className="size-6 accent-orange-600" checked={sizeId === size.id} onChange={() => setSizeId(size.id)} />
              {size.name}
            </span>
            <span>{formatMoney(priceAt(product, size.id)!)}</span>
          </label>
        ))}
      </fieldset>

      {product.allowsHalves && halfCandidates.length > 0 && (
        <div className="flex flex-col gap-2">
          <Checkbox label={t('shop.halfAndHalf')} checked={halves} onChange={(e) => setHalves(e.target.checked)} />
          {halves && (
            <SelectField label={t('shop.secondHalf')} value={secondHalfId} onChange={(e) => setSecondHalfId(e.target.value)}>
              <option value="" disabled>
                —
              </option>
              {halfCandidates.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </SelectField>
          )}
        </div>
      )}

      {groups.map((group) => (
        <fieldset key={group.id} className="flex flex-col gap-1">
          <legend className="mb-1 text-lg font-semibold">
            {group.name}{' '}
            <span className="text-base font-normal text-neutral-600">({selectionLabel(t, group)})</span>
          </legend>
          {group.modifiers.map((modifier) => {
            const checked = validModifierIds.includes(modifier.id);
            const groupFull = group.maxSelect > 1 && group.modifiers.filter((m) => validModifierIds.includes(m.id)).length >= group.maxSelect;
            const price = modifier.prices.find((p) => p.sizeId === sizeId)?.price;
            if (price === undefined) {
              return null;
            }
            return (
              <label key={modifier.id} className="flex min-h-12 items-center justify-between gap-3 rounded-lg bg-white px-3 text-lg">
                <span className="flex items-center gap-3">
                  <input
                    type={group.maxSelect === 1 ? 'radio' : 'checkbox'}
                    name={`group-${group.id}`}
                    className="size-6 accent-orange-600"
                    checked={checked}
                    disabled={!checked && groupFull}
                    onChange={(e) => toggle(group, modifier.id, e.target.checked)}
                  />
                  {modifier.name}
                </span>
                <span>+{formatMoney(price)}</span>
              </label>
            );
          })}
        </fieldset>
      ))}

      <div className="flex items-center gap-3">
        <span className="text-lg font-semibold">{t('shop.quantity')}</span>
        <Button variant="secondary" aria-label={t('shop.decrease')} disabled={quantity <= 1} onClick={() => setQuantity((q) => q - 1)}>
          −
        </Button>
        <span className="min-w-8 text-center text-xl font-bold" aria-live="polite">
          {quantity}
        </span>
        <Button variant="secondary" aria-label={t('shop.increase')} disabled={quantity >= 99} onClick={() => setQuantity((q) => q + 1)}>
          +
        </Button>
      </div>

      <Button className="w-full" disabled={!canAdd} onClick={add}>
        {t('shop.addToCart', { price: formatMoney((unitPrice ?? 0) * quantity) })}
      </Button>
    </div>
  );
}

function selectionLabel(t: ReturnType<typeof useTranslation>['t'], group: ModifierGroupDto): string {
  if (group.minSelect === group.maxSelect) {
    return t('shop.chooseExactly', { count: group.minSelect });
  }
  return group.minSelect === 0 ? t('shop.chooseUpTo', { count: group.maxSelect }) : t('shop.chooseBetween', { min: group.minSelect, max: group.maxSelect });
}
