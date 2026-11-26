import {
  allowedGroups,
  previewUnitPrice,
  priceAt,
  selectionIsComplete,
  unwrap,
  type FulfillmentType,
  type ItemChoice,
  type MenuDto,
  type PaymentMethod,
  type PlaceOrderRequest,
} from '@pizzeria/api-client';
import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, Checkbox, SelectField, TextField } from '@pizzeria/ui';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent, type KeyboardEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { useServices } from '../app/services';
import { useFormErrors } from '../lib/formErrors';
import { useMenu } from '../menu/queries';
import { useDeliveryCheck } from '../zones/queries';
import { ordersKey, useCurrentLocationId } from './queries';

type Line = ItemChoice & { key: string; quantity: number; notes: string };

let nextKey = 0;

/** Phone order: optimised for typing while talking — phone first, Enter adds an item, Ctrl+Enter places the order. */
export function PhoneOrderPage() {
  const { t } = useTranslation();
  const menu = useMenu();
  if (menu.data === undefined) {
    return <p className="text-lg">{t('common.loading')}</p>;
  }
  return <PhoneOrderForm menu={menu.data} />;
}

function PhoneOrderForm({ menu: fullMenu }: { menu: MenuDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { api } = useServices();
  const queryClient = useQueryClient();
  const [locationId] = useCurrentLocationId();
  const check = useDeliveryCheck();
  const menu = { ...fullMenu, products: fullMenu.products.filter((p) => p.isActive) };

  const place = useMutation({
    mutationFn: (body: PlaceOrderRequest) =>
      unwrap(api.POST('/api/locations/{locationId}/orders', { params: { path: { locationId } }, body })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ordersKey(locationId) });
      void navigate('/orders');
    },
  });
  const errors = useFormErrors(place.error);

  const [phone, setPhone] = useState('');
  const [name, setName] = useState('');
  const [fulfillment, setFulfillment] = useState<FulfillmentType>('Delivery');
  const [street, setStreet] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [city, setCity] = useState('');
  const [addressNotes, setAddressNotes] = useState('');
  const [payment, setPayment] = useState<PaymentMethod>('Cash');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<Line[]>([]);
  const [noItems, setNoItems] = useState(false);

  const itemsTotal = lines.reduce((sum, l) => sum + (previewUnitPrice(menu, l) ?? 0) * l.quantity, 0);
  const deliveryFee = fulfillment === 'Delivery' && check.data?.delivers ? Number(check.data.deliveryFee) : 0;

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    setNoItems(lines.length === 0);
    if (lines.length === 0) {
      return;
    }
    place.mutate({
      fulfillment,
      pickupLocationId: fulfillment === 'Pickup' ? locationId : null,
      deliveryAddress: fulfillment === 'Delivery' ? { street, postalCode, city, notes: addressNotes || null } : null,
      customer: { name, phone },
      paymentMethod: payment,
      notes: notes || null,
      items: lines.map((l) => ({
        productId: l.productId,
        secondHalfProductId: l.secondHalfProductId,
        sizeId: l.sizeId,
        quantity: l.quantity,
        modifierIds: l.modifierIds,
        notes: l.notes || null,
      })),
    });
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      submit();
    }
  };

  return (
    <form onSubmit={submit} onKeyDown={onKeyDown} className="flex flex-col gap-4" noValidate>
      <h1 className="text-3xl font-bold">{t('phoneOrder.title')}</h1>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <TextField label={t('phoneOrder.phone')} type="tel" autoFocus value={phone} onChange={(e) => setPhone(e.target.value)} error={errors.field('customer.phone')} />
        <TextField label={t('phoneOrder.name')} value={name} onChange={(e) => setName(e.target.value)} error={errors.field('customer.name')} />
      </div>

      <div className="flex flex-wrap gap-2">
        {(['Delivery', 'Pickup'] as const).map((option) => (
          <Button key={option} variant={fulfillment === option ? 'primary' : 'secondary'} aria-pressed={fulfillment === option} onClick={() => setFulfillment(option)}>
            {option === 'Delivery' ? t('shop.delivery') : t('phoneOrder.pickupHere')}
          </Button>
        ))}
      </div>

      {fulfillment === 'Delivery' && (
        <div className="flex flex-col gap-3 rounded-xl bg-white p-3 shadow-sm">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-[2fr_8rem_1fr]">
            <TextField label={t('shop.street')} value={street} onChange={(e) => setStreet(e.target.value)} error={errors.field('deliveryAddress.street')} />
            <TextField label={t('shop.postalCode')} value={postalCode} onChange={(e) => setPostalCode(e.target.value)} error={errors.field('deliveryAddress.postalCode')} />
            <TextField label={t('shop.city')} value={city} onChange={(e) => setCity(e.target.value)} error={errors.field('deliveryAddress.city')} />
          </div>
          <TextField label={t('shop.addressNotes')} value={addressNotes} onChange={(e) => setAddressNotes(e.target.value)} />
          <Button variant="secondary" className="self-start" onClick={() => check.mutate({ street, postalCode, city })}>
            {t('shop.checkAddress')}
          </Button>
          {check.data && (
            <p role="status" className={`text-lg font-semibold ${check.data.delivers ? 'text-green-800' : 'text-red-800'}`}>
              {check.data.delivers
                ? t('shop.deliveryTerms', { location: check.data.locationName ?? '', fee: formatMoney(check.data.deliveryFee ?? 0), minutes: check.data.estimatedMinutes ?? 0 })
                : t('shop.outsideZone')}
            </p>
          )}
        </div>
      )}

      <ItemEditor menu={menu} onAdd={(line) => setLines((current) => [...current, line])} />

      <section className="flex flex-col gap-2">
        <h2 className="text-xl font-bold">{t('phoneOrder.items')}</h2>
        {noItems && <Alert>{t('phoneOrder.noItems')}</Alert>}
        <ul className="flex flex-col gap-1">
          {lines.map((line) => (
            <li key={line.key} className="flex items-center justify-between gap-3 rounded-lg bg-white px-3 py-2 text-lg">
              <span>
                <strong>{line.quantity}×</strong> {describe(menu, line)}
                {line.notes && <span className="block text-sm italic">{line.notes}</span>}
              </span>
              <span className="flex items-center gap-3">
                {formatMoney((previewUnitPrice(menu, line) ?? 0) * line.quantity)}
                <Button variant="secondary" onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}>
                  {t('common.remove')}
                </Button>
              </span>
            </li>
          ))}
        </ul>
      </section>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <SelectField label={t('shop.payment')} value={payment} onChange={(e) => setPayment(e.target.value as PaymentMethod)}>
          <option value="Cash">{t('paymentMethod.Cash')}</option>
          <option value="CardOnDelivery">{t('paymentMethod.CardOnDelivery')}</option>
        </SelectField>
        <TextField label={t('shop.notes')} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </div>

      {errors.general && <Alert>{errors.general}</Alert>}
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={place.isPending}>
          {t('phoneOrder.submit', { total: formatMoney(itemsTotal + deliveryFee) })}
        </Button>
        <Link to="/orders" className={buttonClassName('secondary')}>
          {t('common.cancel')}
        </Link>
        <span className="text-sm text-neutral-600">{t('phoneOrder.submitHint')}</span>
      </div>
    </form>
  );
}

function describe(menu: MenuDto, line: ItemChoice): string {
  const name = (id: string | null) => menu.products.find((p) => p.id === id)?.name ?? '?';
  const size = menu.categories.flatMap((c) => c.sizes).find((s) => s.id === line.sizeId)?.name ?? '';
  const modifiers = menu.modifierGroups.flatMap((g) => g.modifiers).filter((m) => line.modifierIds.includes(m.id)).map((m) => m.name);
  const product = line.secondHalfProductId ? `${name(line.productId)} / ${name(line.secondHalfProductId)}` : name(line.productId);
  return [`${product} ${size}`, ...modifiers].join(', ');
}

function ItemEditor({ menu, onAdd }: { menu: MenuDto; onAdd: (line: Line) => void }) {
  const { t } = useTranslation();
  const [productId, setProductId] = useState('');
  const [sizeId, setSizeId] = useState('');
  const [secondHalfId, setSecondHalfId] = useState('');
  const [modifierIds, setModifierIds] = useState<string[]>([]);
  const [quantity, setQuantity] = useState('1');
  const [notes, setNotes] = useState('');

  const product = menu.products.find((p) => p.id === productId);
  const category = menu.categories.find((c) => c.id === product?.categoryId);
  const sizes = category?.sizes.filter((s) => product && priceAt(product, s.id) !== null) ?? [];
  const halfCandidates = product?.allowsHalves
    ? menu.products.filter((p) => p.id !== product.id && p.allowsHalves && p.categoryId === product.categoryId && priceAt(p, sizeId) !== null)
    : [];
  const secondHalf = menu.products.find((p) => p.id === secondHalfId) ?? null;
  const groups = product ? allowedGroups(menu, product, secondHalf) : [];
  const choice: ItemChoice = { productId, secondHalfProductId: secondHalf?.id ?? null, sizeId, modifierIds };
  const qty = Number(quantity);
  const canAdd = product !== undefined && previewUnitPrice(menu, choice) !== null && selectionIsComplete(groups, modifierIds) && qty >= 1 && qty <= 99;

  const pickProduct = (id: string) => {
    const next = menu.products.find((p) => p.id === id);
    setProductId(id);
    setSecondHalfId('');
    setModifierIds([]);
    setSizeId(next?.prices[0]?.sizeId ?? '');
  };

  const add = () => {
    if (!canAdd) {
      return;
    }
    onAdd({ ...choice, key: `line-${nextKey++}`, quantity: qty, notes });
    pickProduct('');
    setQuantity('1');
    setNotes('');
  };

  return (
    <section
      className="flex flex-col gap-3 rounded-xl border-2 border-orange-300 bg-orange-50 p-3"
      onKeyDown={(e) => {
        if (e.key === 'Enter' && !e.ctrlKey && !e.metaKey && e.target instanceof HTMLInputElement) {
          e.preventDefault();
          add();
        }
      }}
    >
      <div className="grid grid-cols-1 gap-3 md:grid-cols-[2fr_1fr_6rem]">
        <SelectField label={t('phoneOrder.product')} value={productId} onChange={(e) => pickProduct(e.target.value)}>
          <option value="">—</option>
          {menu.categories.map((c) => (
            <optgroup key={c.id} label={c.name}>
              {menu.products
                .filter((p) => p.categoryId === c.id)
                .map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
            </optgroup>
          ))}
        </SelectField>
        <SelectField label={t('phoneOrder.size')} value={sizeId} onChange={(e) => setSizeId(e.target.value)} disabled={!product}>
          {sizes.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name} — {formatMoney(priceAt(product!, s.id)!)}
            </option>
          ))}
        </SelectField>
        <TextField label={t('phoneOrder.quantity')} type="number" min={1} max={99} value={quantity} onChange={(e) => setQuantity(e.target.value)} />
      </div>
      {halfCandidates.length > 0 && (
        <SelectField label={t('phoneOrder.secondHalf')} value={secondHalfId} onChange={(e) => { setSecondHalfId(e.target.value); setModifierIds([]); }}>
          <option value="">—</option>
          {halfCandidates.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </SelectField>
      )}
      {groups.map((group) => (
        <fieldset key={group.id} className="flex flex-wrap gap-x-4">
          <legend className="text-base font-semibold">
            {group.name} ({group.minSelect}–{group.maxSelect})
          </legend>
          {group.modifiers
            .filter((m) => m.isActive)
            .map((m) => (
              <Checkbox
                key={m.id}
                label={m.name}
                checked={modifierIds.includes(m.id)}
                onChange={(e) =>
                  setModifierIds((ids) => {
                    if (!e.target.checked) {
                      return ids.filter((id) => id !== m.id);
                    }
                    // Single-choice groups replace the previous choice.
                    const others = group.maxSelect === 1 ? ids.filter((id) => !group.modifiers.some((gm) => gm.id === id)) : ids;
                    return [...others, m.id];
                  })
                }
              />
            ))}
        </fieldset>
      ))}
      <TextField label={t('phoneOrder.itemNotes')} value={notes} onChange={(e) => setNotes(e.target.value)} />
      <Button className="self-start" disabled={!canAdd} onClick={add}>
        {t('phoneOrder.addItem')}
      </Button>
    </section>
  );
}
