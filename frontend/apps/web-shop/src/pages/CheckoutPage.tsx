import type { DeliveryQuoteDto, FulfillmentType, PaymentMethod } from '@pizzeria/api-client';
import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, Checkbox, SelectField, TextField } from '@pizzeria/ui';
import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { useAddAddress, useAddresses, useProfile, useSignedIn } from '../app/account';
import { goToPayment, useDeliveryCheck, usePlaceOrder, useShop } from '../app/services';
import { useCart } from '../cart/cart';
import { useFormErrors } from '../lib/formErrors';
import { cartTotal, describeItem } from './CartPage';

const paymentMethods: PaymentMethod[] = ['Cash', 'CardOnDelivery'];

export function CheckoutPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const shop = useShop().data!;
  const cart = useCart();
  const check = useDeliveryCheck();
  const place = usePlaceOrder();
  const placeErrors = useFormErrors(place.error);
  const checkErrors = useFormErrors(check.error);

  const [fulfillment, setFulfillment] = useState<FulfillmentType>('Delivery');
  const [locationId, setLocationId] = useState(shop.locations[0]?.id ?? '');
  const [street, setStreet] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [city, setCity] = useState('');
  const [addressNotes, setAddressNotes] = useState('');
  // null = not typed yet: a signed-in customer's profile fills in the default.
  const [typedName, setName] = useState<string | null>(null);
  const [typedPhone, setPhone] = useState<string | null>(null);
  const signedIn = useSignedIn();
  const profile = useProfile().data;
  const savedAddresses = useAddresses().data ?? [];
  const addAddress = useAddAddress();
  const [savedAddressId, setSavedAddressId] = useState('');
  const [saveAddress, setSaveAddress] = useState(false);
  const name = typedName ?? profile?.name ?? '';
  const phone = typedPhone ?? profile?.phone ?? '';
  const [payment, setPayment] = useState<PaymentMethod>('Cash');
  const [notes, setNotes] = useState('');

  if (cart.items.length === 0 && !place.isPending && !place.isSuccess) {
    return <Navigate to="/" replace />;
  }

  const itemsTotal = cartTotal(shop.menu, cart.items);
  const quote: DeliveryQuoteDto | undefined = check.data;
  const deliveryFee = fulfillment === 'Delivery' && quote?.delivers ? Number(quote.deliveryFee) : 0;
  const belowMinimum = fulfillment === 'Delivery' && quote?.delivers === true && itemsTotal < Number(quote.minOrderValue);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    place.mutate(
      {
        fulfillment,
        pickupLocationId: fulfillment === 'Pickup' ? locationId : null,
        deliveryAddress: fulfillment === 'Delivery' ? { street, postalCode, city, notes: addressNotes || null } : null,
        customer: { name, phone },
        paymentMethod: payment,
        notes: notes || null,
        // No prices: the API prices the order itself.
        items: cart.items.map((i) => ({
          productId: i.productId,
          secondHalfProductId: i.secondHalfProductId,
          sizeId: i.sizeId,
          quantity: i.quantity,
          modifierIds: i.modifierIds,
          notes: null,
        })),
      },
      {
        onSuccess: (order) => {
          cart.clear();
          if (fulfillment === 'Delivery' && signedIn && saveAddress && savedAddressId === '') {
            addAddress.mutate({ label: null, street, postalCode, city, notes: addressNotes || null });
          }
          if (order.paymentRedirectUrl) {
            goToPayment(order.paymentRedirectUrl, navigate);
          } else {
            void navigate(`/order/${order.trackingToken}`, { replace: true });
          }
        },
      },
    );
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-5" noValidate>
      <h1 className="text-3xl font-bold">{t('shop.checkoutTitle')}</h1>

      <ul className="flex flex-col gap-1 rounded-xl bg-white p-3 shadow-sm">
        {cart.items.map((item) => {
          const { title, details } = describeItem(shop.menu, item);
          return (
            <li key={item.key} className="text-base">
              <strong>{item.quantity}×</strong> {title} <span className="text-neutral-600">({details})</span>
            </li>
          );
        })}
      </ul>

      <div role="radiogroup" className="grid grid-cols-2 gap-2">
        {(['Delivery', 'Pickup'] as const).map((option) => (
          <Choice key={option} name="fulfillment" checked={fulfillment === option} onChange={() => setFulfillment(option)}>
            {option === 'Delivery' ? t('shop.delivery') : t('shop.pickup')}
          </Choice>
        ))}
      </div>

      {!signedIn && (
        <p className="text-base">
          <Link to="/account/login?next=/checkout" className="font-semibold text-orange-700 underline">
            {t('account.checkoutSignIn')}
          </Link>
        </p>
      )}

      {fulfillment === 'Delivery' ? (
        <fieldset className="flex flex-col gap-3">
          {savedAddresses.length > 0 && (
            <SelectField
              label={t('account.savedAddress')}
              value={savedAddressId}
              onChange={(e) => {
                const picked = savedAddresses.find((a) => a.id === e.target.value);
                setSavedAddressId(picked?.id ?? '');
                if (picked) {
                  setStreet(picked.street);
                  setPostalCode(picked.postalCode);
                  setCity(picked.city);
                  setAddressNotes(picked.notes ?? '');
                  check.reset();
                }
              }}
            >
              <option value="">{t('account.newAddress')}</option>
              {savedAddresses.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.label ? `${a.label}: ` : ''}{a.street}, {a.city}
                </option>
              ))}
            </SelectField>
          )}
          <TextField label={t('shop.street')} autoComplete="street-address" value={street} onChange={(e) => { setStreet(e.target.value); setSavedAddressId(''); }} error={placeErrors.field('deliveryAddress.street') ?? checkErrors.field('street')} />
          <div className="grid grid-cols-[8rem_1fr] gap-3">
            <TextField label={t('shop.postalCode')} autoComplete="postal-code" inputMode="numeric" value={postalCode} onChange={(e) => { setPostalCode(e.target.value); setSavedAddressId(''); }} error={placeErrors.field('deliveryAddress.postalCode') ?? checkErrors.field('postalCode')} />
            <TextField label={t('shop.city')} autoComplete="address-level2" value={city} onChange={(e) => { setCity(e.target.value); setSavedAddressId(''); }} error={placeErrors.field('deliveryAddress.city') ?? checkErrors.field('city')} />
          </div>
          <TextField label={t('shop.addressNotes')} value={addressNotes} onChange={(e) => setAddressNotes(e.target.value)} />
          {signedIn && savedAddressId === '' && (
            <Checkbox label={t('account.saveForLater')} checked={saveAddress} onChange={(e) => setSaveAddress(e.target.checked)} />
          )}
          <Button variant="secondary" className="self-start" disabled={check.isPending} onClick={() => check.mutate({ street, postalCode, city })}>
            {t('shop.checkAddress')}
          </Button>
          {check.isError && <Alert>{checkErrors.general}</Alert>}
          {quote && (
            <p role="status" className={`text-lg font-semibold ${quote.delivers ? 'text-green-800' : 'text-red-800'}`}>
              {quote.delivers
                ? t('shop.deliveryTerms', { location: quote.locationName ?? '', fee: formatMoney(quote.deliveryFee ?? 0), minutes: quote.estimatedMinutes ?? 0 })
                : t('shop.outsideZone')}
            </p>
          )}
          {belowMinimum && <Alert>{t('shop.minimumOrder', { min: formatMoney(quote!.minOrderValue ?? 0) })}</Alert>}
        </fieldset>
      ) : (
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-lg font-semibold">{t('shop.pickupLocation')}</legend>
          {shop.locations.map((location) => (
            <Choice key={location.id} name="location" checked={locationId === location.id} onChange={() => setLocationId(location.id)}>
              <span className="block">{location.name}</span>
              <span className="block text-sm font-normal">{location.address.street}, {location.address.city}</span>
            </Choice>
          ))}
        </fieldset>
      )}

      <TextField label={t('shop.name')} autoComplete="given-name" value={name} onChange={(e) => setName(e.target.value)} error={placeErrors.field('customer.name')} />
      <TextField label={t('shop.phone')} type="tel" autoComplete="tel" value={phone} onChange={(e) => setPhone(e.target.value)} error={placeErrors.field('customer.phone')} />

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-lg font-semibold">{t('shop.payment')}</legend>
        {(shop.onlinePayments ? [...paymentMethods, 'Online' as const] : paymentMethods).map((method) => (
          <Choice key={method} name="payment" checked={payment === method} onChange={() => setPayment(method)}>
            {t(`paymentMethod.${method}`)}
          </Choice>
        ))}
      </fieldset>

      <TextField label={t('shop.notes')} value={notes} onChange={(e) => setNotes(e.target.value)} />

      <dl className="flex flex-col gap-1 text-lg">
        <div className="flex justify-between"><dt>{t('shop.itemsTotal')}</dt><dd>{formatMoney(itemsTotal)}</dd></div>
        {fulfillment === 'Delivery' && quote?.delivers && (
          <div className="flex justify-between"><dt>{t('shop.deliveryFee')}</dt><dd>{formatMoney(deliveryFee)}</dd></div>
        )}
        <div className="flex justify-between text-xl font-bold"><dt>{t('shop.total')}</dt><dd>{formatMoney(itemsTotal + deliveryFee)}</dd></div>
        <p className="text-sm text-neutral-600">{t('shop.totalPreview')}</p>
      </dl>

      {placeErrors.general && <Alert>{placeErrors.general}</Alert>}
      <Button type="submit" className="w-full" disabled={place.isPending}>
        {place.isPending
          ? t('shop.placing')
          : t(payment === 'Online' ? 'shop.placeOrderOnline' : 'shop.placeOrder', { total: formatMoney(itemsTotal + deliveryFee) })}
      </Button>
    </form>
  );
}

function Choice({ name, checked, onChange, children }: { name: string; checked: boolean; onChange: () => void; children: ReactNode }) {
  return (
    <label
      className={`flex min-h-14 cursor-pointer items-center gap-3 rounded-xl border-2 px-3 text-lg font-semibold ${
        checked ? 'border-orange-600 bg-orange-50' : 'border-neutral-300 bg-white'
      }`}
    >
      <input type="radio" name={name} className="size-6 accent-orange-600" checked={checked} onChange={onChange} />
      <span>{children}</span>
    </label>
  );
}
