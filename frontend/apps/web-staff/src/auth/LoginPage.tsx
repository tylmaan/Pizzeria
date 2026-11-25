import { errorCodeOf } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button, TextField } from '@pizzeria/ui';
import { useMutation } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { useServices } from '../app/services';
import type { LoginRedirectState } from './RequireAuth';
import { useSignedIn } from './session';

export function LoginPage() {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const { session } = useServices();
  const signedIn = useSignedIn();
  const navigate = useNavigate();
  const from = (useLocation().state as LoginRedirectState | null)?.from ?? '/';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const login = useMutation({
    mutationFn: () => session.login(email.trim(), password),
    onSuccess: () => void navigate(from, { replace: true }),
  });

  if (signedIn && !login.isPending) {
    return <Navigate to={from} replace />;
  }

  const submit = (event: FormEvent) => {
    event.preventDefault();
    login.mutate();
  };

  return (
    <main className="flex min-h-screen items-center justify-center p-4">
      <form onSubmit={submit} className="flex w-full max-w-sm flex-col gap-5 rounded-xl bg-white p-6 shadow-lg">
        <div>
          <p className="text-lg font-semibold text-orange-700">{t('app.staffTitle')}</p>
          <h1 className="text-3xl font-bold">{t('auth.title')}</h1>
        </div>
        {login.isError && <Alert>{codes.error(errorCodeOf(login.error))}</Alert>}
        <TextField
          label={t('auth.email')}
          type="email"
          autoComplete="username"
          required
          autoFocus
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <TextField
          label={t('auth.password')}
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <Button type="submit" disabled={login.isPending} className="w-full">
          {login.isPending ? t('auth.submitting') : t('auth.submit')}
        </Button>
      </form>
    </main>
  );
}
