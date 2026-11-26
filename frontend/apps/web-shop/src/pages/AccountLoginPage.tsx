import { useTranslation } from '@pizzeria/i18n';
import { Alert, Button, TextField } from '@pizzeria/ui';
import { useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router';
import { useLogin, useRegister, useSignedIn } from '../app/account';
import { useFormErrors } from '../lib/formErrors';

/** Sign in or create an account; afterwards back to where the customer came from (e.g. the checkout). */
export function AccountLoginPage() {
  const { t } = useTranslation();
  const signedIn = useSignedIn();
  const next = useSafeNext();

  if (signedIn) {
    return <Navigate to={next} replace />;
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-lg">{t('account.why')}</p>
      <LoginForm next={next} />
      <RegisterForm next={next} />
    </div>
  );
}

function LoginForm({ next }: { next: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const login = useLogin();
  const errors = useFormErrors(login.error);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  return (
    <form
      noValidate
      className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm"
      onSubmit={(e) => {
        e.preventDefault();
        login.mutate({ email, password }, { onSuccess: () => void navigate(next, { replace: true }) });
      }}
    >
      <h1 className="text-2xl font-bold">{t('account.loginTitle')}</h1>
      {errors.general && <Alert>{errors.general}</Alert>}
      <TextField label={t('auth.email')} type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      <TextField label={t('auth.password')} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      <Button type="submit" disabled={login.isPending || email === '' || password === ''}>
        {login.isPending ? t('auth.submitting') : t('auth.submit')}
      </Button>
    </form>
  );
}

function RegisterForm({ next }: { next: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const register = useRegister();
  const errors = useFormErrors(register.error);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');

  return (
    <form
      noValidate
      className="flex flex-col gap-3 rounded-xl bg-white p-4 shadow-sm"
      onSubmit={(e) => {
        e.preventDefault();
        register.mutate(
          { name, email, phone: phone.trim() || null, password },
          { onSuccess: () => void navigate(next, { replace: true }) },
        );
      }}
    >
      <h2 className="text-2xl font-bold">{t('account.registerTitle')}</h2>
      {errors.general && <Alert>{errors.general}</Alert>}
      <TextField label={t('shop.name')} autoComplete="given-name" value={name} onChange={(e) => setName(e.target.value)} error={errors.field('name')} />
      <TextField label={t('auth.email')} type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} error={errors.field('email')} />
      <TextField label={t('account.phoneOptional')} type="tel" autoComplete="tel" value={phone} onChange={(e) => setPhone(e.target.value)} error={errors.field('phone')} />
      <TextField label={t('auth.password')} type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} error={errors.field('password')} />
      <p className="text-sm text-neutral-600">{t('account.passwordHint')}</p>
      <Button type="submit" disabled={register.isPending}>
        {t('account.register')}
      </Button>
    </form>
  );
}

/** Only paths inside the shop: `?next=https://evil.example` must not turn the login into an open redirect. */
function useSafeNext(): string {
  const [params] = useSearchParams();
  const next = params.get('next') ?? '/account';
  return next.startsWith('/') && !next.startsWith('//') ? next : '/account';
}
