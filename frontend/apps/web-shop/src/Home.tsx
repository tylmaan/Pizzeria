import { useTranslation } from '@pizzeria/i18n';

export function Home() {
  const { t } = useTranslation();

  return (
    <main className="mx-auto max-w-xl p-4">
      <h1 className="mb-2 text-3xl font-bold">{t('app.shopTitle')}</h1>
      <p className="text-lg">{t('placeholder.comingSoon')}</p>
    </main>
  );
}
