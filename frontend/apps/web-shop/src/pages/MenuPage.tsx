import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Link } from 'react-router';
import { useShop } from '../app/services';
import { lowestPrice } from '../menu/pricing';

export function MenuPage() {
  const { t } = useTranslation();
  const menu = useShop().data!.menu;

  return (
    <div className="flex flex-col gap-6">
      {menu.categories.map((category) => (
        <section key={category.id}>
          <h2 className="mb-2 text-2xl font-bold">{category.name}</h2>
          <ul className="flex flex-col gap-2">
            {menu.products
              .filter((p) => p.categoryId === category.id)
              .map((product) => (
                <li key={product.id}>
                  <Link
                    to={`/product/${product.id}`}
                    className="flex min-h-16 items-center justify-between gap-3 rounded-xl bg-white p-4 shadow-sm active:bg-orange-50"
                  >
                    <span>
                      <span className="block text-lg font-semibold">{product.name}</span>
                      {product.description && <span className="block text-sm text-neutral-600">{product.description}</span>}
                    </span>
                    <span className="whitespace-nowrap text-base font-semibold text-orange-700">
                      {product.prices.length > 1
                        ? t('shop.priceFrom', { price: formatMoney(lowestPrice(product)) })
                        : formatMoney(lowestPrice(product))}
                    </span>
                  </Link>
                </li>
              ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
