import { errorCodeOf, type Role } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert, Button } from '@pizzeria/ui';
import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router';
import { CurrentUserContext, hasAnyRole, useCurrentUser, useMeQuery, useSignedIn } from './session';

export type LoginRedirectState = { from?: string };

/** Signed-out users go to the login screen; the page they wanted is restored after signing in. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const signedIn = useSignedIn();
  const location = useLocation();
  const me = useMeQuery();

  if (!signedIn) {
    const state: LoginRedirectState = { from: location.pathname };
    return <Navigate to="/login" replace state={state} />;
  }

  if (me.isError) {
    return (
      <CenteredMessage>
        <Alert>{codes.error(errorCodeOf(me.error))}</Alert>
        <Button onClick={() => void me.refetch()}>{t('common.retry')}</Button>
      </CenteredMessage>
    );
  }

  if (me.data === undefined) {
    return <CenteredMessage>{t('common.loading')}</CenteredMessage>;
  }

  return <CurrentUserContext.Provider value={me.data}>{children}</CurrentUserContext.Provider>;
}

/**
 * Hides a section from roles that cannot use it (renders nested routes when used without children).
 * The API enforces the same rules independently.
 */
export function RequireRole({ roles, children }: { roles: readonly Role[]; children?: ReactNode }) {
  const user = useCurrentUser();
  if (!hasAnyRole(user, roles)) {
    return <Navigate to="/" replace />;
  }
  return children ?? <Outlet />;
}

export function CenteredMessage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 p-4 text-xl">{children}</div>
  );
}
