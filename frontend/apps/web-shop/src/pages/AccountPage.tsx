import { errorCodeOf, type CustomerProfileDto } from '@pizzeria/api-client';
import { formatMoney, useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { Link, Navigate } from 'react-router';
import {
  useAddAddress,
  useAddresses,
  useDeleteAccount,
  useDeleteAddress,
  useLogout,
  useMyOrders,
  useProfile,
  useSignedIn,
  useUpdateProfile,
} from '../app/account';
import { useFormErrors } from '../lib/formErrors';

const date = (iso: string) =>
  new Date(iso).toLocaleString('pl-PL', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

export function AccountPage() {
  const { t } = useTranslation();
  const signedIn = useSignedIn();
  const profile = useProfile();
  const logout = useLogout();

  if (!signedIn) {
    return <Navigate to="/account/login" replace />;
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-3xl font-bold">{t('account.title')}</h1>
        <Button variant="secondary" onClick={logout}>
          {t('auth.logout')}
        </Button>
      </div>
      <Orders />
      {profile.data && <Profile key={profile.data.email} profile={profile.data} />}
      <Addresses />
      <DeleteAccount />
    </div>
  );
}

function Orders() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const orders = useMyOrders();

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-2xl font-bold">{t('account.orders')}</h2>
      {orders.error && <Alert>{codes.error(errorCodeOf(orders.error))}</Alert>}
      {orders.data?.length === 0 && <p className="text-lg">{t('account.noOrders')}</p>}
      <ul className="flex flex-col gap-2">
        {orders.data?.map((order) => (
          <li key={order.id} aria-label={`#${order.number}`} className="flex flex-col gap-1 rounded-xl bg-white p-3 shadow-sm">
            <div className="flex flex-wrap justify-between gap-2 text-lg">
              <span className="font-bold">#{order.number} · {date(order.placedAt)}</span>
              <span>{formatMoney(order.total)}</span>
            </div>
            <p className="text-base text-neutral-700">{order.items.join(', ')}</p>
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold">{t(`orderStatus.${order.status}`)}</span>
              <Link to={`/order/${order.trackingToken}`} className="font-semibold text-orange-700 underline">
                {t('account.track')}
              </Link>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}

function Profile({ profile }: { profile: CustomerProfileDto }) {
  const { t } = useTranslation();
  const update = useUpdateProfile();
  const errors = useFormErrors(update.error);
  const [name, setName] = useState(profile.name);
  const [phone, setPhone] = useState(profile.phone ?? '');

  return (
    <form
      className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm"
      onSubmit={(e) => {
        e.preventDefault();
        update.mutate({ name, phone: phone.trim() || null });
      }}
    >
      <h2 className="text-2xl font-bold">{t('account.profile')}</h2>
      <p className="text-base text-neutral-700">{profile.email}</p>
      {errors.general && <Alert>{errors.general}</Alert>}
      {update.isSuccess && <Alert tone="success">{t('account.saved')}</Alert>}
      <TextField label={t('shop.name')} value={name} onChange={(e) => setName(e.target.value)} error={errors.field('name')} />
      <TextField label={t('account.phoneOptional')} type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} error={errors.field('phone')} />
      <Button type="submit" className="self-start" disabled={update.isPending}>
        {t('common.save')}
      </Button>
    </form>
  );
}

function Addresses() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const addresses = useAddresses();
  const remove = useDeleteAddress();
  const add = useAddAddress();
  const errors = useFormErrors(add.error);
  const [label, setLabel] = useState('');
  const [street, setStreet] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [city, setCity] = useState('');
  const [notes, setNotes] = useState('');

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-2xl font-bold">{t('account.addresses')}</h2>
      {remove.error && <Alert>{codes.error(errorCodeOf(remove.error))}</Alert>}
      <ul className="flex flex-col gap-2">
        {addresses.data?.map((a) => (
          <li key={a.id} className="flex items-center justify-between gap-3 rounded-xl bg-white p-3 shadow-sm">
            <span className="text-lg">
              {a.label && <strong>{a.label}: </strong>}
              {a.street}, {a.postalCode} {a.city}
              {a.notes && <span className="block text-base text-neutral-600">{a.notes}</span>}
            </span>
            <Button variant="secondary" disabled={remove.isPending} onClick={() => remove.mutate(a.id)}>
              {t('account.remove')}
            </Button>
          </li>
        ))}
      </ul>
      <form
        className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm"
        onSubmit={(e) => {
          e.preventDefault();
          add.mutate(
            { label: label.trim() || null, street, postalCode, city, notes: notes.trim() || null },
            {
              onSuccess: () => {
                setLabel('');
                setStreet('');
                setPostalCode('');
                setCity('');
                setNotes('');
              },
            },
          );
        }}
      >
        <h3 className="text-xl font-semibold">{t('account.addAddress')}</h3>
        {errors.general && <Alert>{errors.general}</Alert>}
        <TextField label={t('account.label')} value={label} onChange={(e) => setLabel(e.target.value)} error={errors.field('label')} />
        <TextField label={t('shop.street')} value={street} onChange={(e) => setStreet(e.target.value)} error={errors.field('street')} />
        <div className="grid grid-cols-[8rem_1fr] gap-3">
          <TextField label={t('shop.postalCode')} inputMode="numeric" value={postalCode} onChange={(e) => setPostalCode(e.target.value)} error={errors.field('postalCode')} />
          <TextField label={t('shop.city')} value={city} onChange={(e) => setCity(e.target.value)} error={errors.field('city')} />
        </div>
        <TextField label={t('shop.addressNotes')} value={notes} onChange={(e) => setNotes(e.target.value)} error={errors.field('notes')} />
        <Button type="submit" className="self-start" disabled={add.isPending}>
          {t('account.saveAddress')}
        </Button>
      </form>
    </section>
  );
}

function DeleteAccount() {
  const { t } = useTranslation();
  const remove = useDeleteAccount();
  const errors = useFormErrors(remove.error);
  const [confirming, setConfirming] = useState(false);

  return (
    <section className="flex flex-col gap-2">
      {errors.general && <Alert>{errors.general}</Alert>}
      {confirming ? (
        <div className="flex flex-col gap-2 rounded-xl bg-red-50 p-4">
          <p className="text-lg">{t('account.deleteWarning')}</p>
          <div className="flex gap-2">
            <Button variant="danger" disabled={remove.isPending} onClick={() => remove.mutate()}>
              {t('account.deleteConfirm')}
            </Button>
            <Button variant="secondary" onClick={() => setConfirming(false)}>
              {t('common.cancel')}
            </Button>
          </div>
        </div>
      ) : (
        <Button variant="secondary" className="self-start" onClick={() => setConfirming(true)}>
          {t('account.delete')}
        </Button>
      )}
    </section>
  );
}
