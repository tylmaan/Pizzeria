import type { MenuDto } from '@pizzeria/api-client';
import { formatMoney, useTranslation } from '@pizzeria/i18n';
import { Button, buttonClassName } from '@pizzeria/ui';
import { Link } from 'react-router';
import { useShop } from '../app/services';
import { useCart, type CartItem } from '../cart/cart';
import { previewUnitPrice } from '../menu/pricing';

/** Human description of a cart line: "Margherita / Pepperoni, 42 cm, Czosnkowy". */
export function describeItem(menu: MenuDto, item: CartItem): { title: string; details: string } {
  const name = (id: string | null) => menu.products.find((p) => p.id === id)?.name ?? '?';
  const size = menu.categories.flatMap((c) => c.sizes).find((s) => s.id === item.sizeId)?.name ?? '';
  const modifiers = menu.modifierGroups.flatMap((g) => g.modifiers).filter((m) => item.modifierIds.includes(m.id)).map((m) => m.name);
  return {
    title: item.secondHalfProductId ? `${name(item.productId)} / ${name(item.secondHalfProductId)}` : name(item.productId),
    details: [size, ...modifiers].join(', '),
  };
}

export function cartTotal(menu: MenuDto, items: CartItem[]): number {
  return items.reduce((sum, i) => sum + (previewUnitPrice(menu, i) ?? 0) * i.quantity, 0);
}

export function CartPage() {
  const { t } = useTranslation();
  const menu = useShop().data!.menu;
  const cart = useCart();

  if (cart.items.length === 0) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-3xl font-bold">{t('shop.cart')}</h1>
        <p className="text-lg">{t('shop.emptyCart')}</p>
        <Link to="/" className={buttonClassName()}>
          {t('shop.backToMenu')}
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-3xl font-bold">{t('shop.cart')}</h1>
      <ul className="flex flex-col gap-2">
        {cart.items.map((item) => {
          const { title, details } = describeItem(menu, item);
          const unit = previewUnitPrice(menu, item) ?? 0;
          return (
            <li key={item.key} className="flex flex-col gap-2 rounded-xl bg-white p-3 shadow-sm">
              <div className="flex justify-between gap-3">
                <span>
                  <span className="block text-lg font-semibold">{title}</span>
                  <span className="block text-sm text-neutral-600">{details}</span>
                </span>
                <span className="whitespace-nowrap text-lg font-semibold">{formatMoney(unit * item.quantity)}</span>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="secondary" aria-label={t('shop.decrease')} disabled={item.quantity <= 1} onClick={() => cart.setQuantity(item.key, item.quantity - 1)}>
                  −
                </Button>
                <span className="min-w-8 text-center text-lg font-bold">{item.quantity}</span>
                <Button variant="secondary" aria-label={t('shop.increase')} onClick={() => cart.setQuantity(item.key, item.quantity + 1)}>
                  +
                </Button>
                <Button variant="secondary" className="ml-auto" onClick={() => cart.remove(item.key)}>
                  {t('shop.remove')}
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
      <p className="flex justify-between text-xl font-bold">
        <span>{t('shop.itemsTotal')}</span>
        <span>{formatMoney(cartTotal(menu, cart.items))}</span>
      </p>
      <Link to="/checkout" className={buttonClassName('primary', 'w-full')}>
        {t('shop.checkout')}
      </Link>
    </div>
  );
}
