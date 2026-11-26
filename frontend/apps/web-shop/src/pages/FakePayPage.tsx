import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Alert, Button } from '@pizzeria/ui';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { useCompleteFakePayment } from '../app/services';

/**
 * Stand-in for an external payment operator page (development only). A real operator shows its own page and
 * then redirects back; here the buttons send the operator's signed webhook through the API and return.
 */
export function FakePayPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { providerPaymentId = '' } = useParams();
  const [query] = useSearchParams();
  const complete = useCompleteFakePayment(providerPaymentId);
  // Only paths inside this shop: never redirect to an address taken from the URL.
  const returnPath = query.get('return')?.startsWith('/') ? query.get('return')! : '/';

  const finish = (succeeded: boolean) => complete.mutate(succeeded, { onSuccess: () => void navigate(returnPath, { replace: true }) });

  return (
    <section className="flex flex-col gap-4 rounded-2xl border-4 border-dashed border-purple-500 bg-white p-5">
      <h1 className="text-3xl font-bold">{t('fakePay.title')}</h1>
      <p className="text-base text-neutral-700">{t('fakePay.hint')}</p>
      <p className="text-2xl font-bold">{t('fakePay.amount', { amount: formatMoney(Number(query.get('amount') ?? 0)) })}</p>
      {complete.isError && <Alert>{t('errors.common.unexpected')}</Alert>}
      <Button className="min-h-14 text-xl" disabled={complete.isPending} onClick={() => finish(true)}>
        {t('fakePay.pay')}
      </Button>
      <Button variant="secondary" disabled={complete.isPending} onClick={() => finish(false)}>
        {t('fakePay.reject')}
      </Button>
    </section>
  );
}
