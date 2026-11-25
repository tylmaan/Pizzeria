import i18n from 'i18next';
import { initReactI18next, useTranslation } from 'react-i18next';
import plCommon from './locales/pl/common.json';

export const defaultLanguage = 'pl';
export const supportedLanguages = ['pl'] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

export const resources = {
  pl: { common: plCommon },
} as const;

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common';
    resources: (typeof resources)['pl'];
  }
}

/** Initializes the shared i18next instance. Safe to call more than once. */
export function initI18n(language: SupportedLanguage = defaultLanguage) {
  if (!i18n.isInitialized) {
    void i18n.use(initReactI18next).init({
      resources,
      lng: language,
      fallbackLng: defaultLanguage,
      defaultNS: 'common',
      interpolation: { escapeValue: false },
      initAsync: false,
    });
  }
  return i18n;
}

/**
 * Translates codes that come from the API at runtime (so they cannot be checked at compile time):
 * error codes (`auth.invalid_credentials` → `errors.auth.invalid_credentials`), validation codes
 * (`validation.not_empty`) and role names. Unknown codes fall back to a generic text.
 */
export function useCodeTranslations() {
  const { t } = useTranslation();
  const translate = (key: string, fallbackKey: string): string =>
    // Keys are runtime strings here; the typed `t` only accepts literal keys.
    i18n.exists(key) ? t(key as never) : t(fallbackKey as never);

  return {
    error: (code: string) => translate(`errors.${code}`, 'errors.common.unexpected'),
    validation: (code: string) => translate(code, 'validation.invalid'),
    role: (role: string) => (i18n.exists(`roles.${role}`) ? t(`roles.${role}` as never) : role),
  };
}

export { i18n };
export { Trans, useTranslation } from 'react-i18next';
