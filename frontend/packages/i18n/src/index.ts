import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
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

export { i18n };
export { Trans, useTranslation } from 'react-i18next';
