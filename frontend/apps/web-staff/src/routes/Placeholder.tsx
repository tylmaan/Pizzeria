import { useTranslation } from '@pizzeria/i18n';

type Props = {
  titleKey: 'nav.orders' | 'nav.kitchen' | 'nav.driver';
};

export function Placeholder({ titleKey }: Props) {
  const { t } = useTranslation();

  return (
    <section>
      <h1 className="mb-2 text-3xl font-bold">{t(titleKey)}</h1>
      <p className="text-lg">{t('placeholder.comingSoon')}</p>
    </section>
  );
}
