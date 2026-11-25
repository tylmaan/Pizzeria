import { errorCodeOf, type MenuDto } from '@pizzeria/api-client';
import { useCodeTranslations, useTranslation } from '@pizzeria/i18n';
import { Alert } from '@pizzeria/ui';
import type { ReactNode } from 'react';
import { QueryState } from '../company/PageHeader';
import { useMenu } from './queries';

/** Loads the menu and renders an edit form for one of its entities (or a not-found message). */
export function MenuEntityLoader<T>({
  find,
  children,
}: {
  find: (menu: MenuDto) => T | undefined;
  children: (entity: T, menu: MenuDto) => ReactNode;
}) {
  const { t } = useTranslation();
  const codes = useCodeTranslations();
  const menu = useMenu();

  if (menu.isError) {
    return <Alert>{codes.error(errorCodeOf(menu.error))}</Alert>;
  }
  if (menu.data === undefined) {
    return <QueryState>{t('common.loading')}</QueryState>;
  }
  const entity = find(menu.data);
  return entity === undefined ? <Alert>{codes.error('common.not_found')}</Alert> : children(entity, menu.data);
}
