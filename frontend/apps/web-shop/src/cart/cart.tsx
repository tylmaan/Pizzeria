import { createContext, useContext, useEffect, useReducer, type ReactNode } from 'react';
import type { ItemChoice } from '../menu/pricing';

export type CartItem = ItemChoice & { key: string; quantity: number };

type Action =
  | { type: 'add'; item: CartItem }
  | { type: 'quantity'; key: string; quantity: number }
  | { type: 'remove'; key: string }
  | { type: 'clear' };

function reducer(items: CartItem[], action: Action): CartItem[] {
  switch (action.type) {
    case 'add':
      return [...items, action.item];
    case 'quantity':
      return items.map((i) => (i.key === action.key ? { ...i, quantity: Math.min(99, Math.max(1, action.quantity)) } : i));
    case 'remove':
      return items.filter((i) => i.key !== action.key);
    case 'clear':
      return [];
  }
}

/** Where the cart survives a reload. Per shop host, so two pizzerias never share a cart. */
export interface CartStore {
  load(): CartItem[];
  save(items: CartItem[]): void;
}

export function localStorageCartStore(key = `pizzeria.cart.${globalThis.location?.host ?? ''}`): CartStore {
  return {
    load: () => {
      try {
        const parsed: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');
        return Array.isArray(parsed) ? (parsed as CartItem[]) : [];
      } catch {
        return [];
      }
    },
    save: (items) => {
      try {
        localStorage.setItem(key, JSON.stringify(items));
      } catch {
        // Storage unavailable: the cart lives only in this tab.
      }
    },
  };
}

type CartApi = {
  items: CartItem[];
  count: number;
  add: (choice: ItemChoice, quantity: number) => void;
  setQuantity: (key: string, quantity: number) => void;
  remove: (key: string) => void;
  clear: () => void;
};

const CartContext = createContext<CartApi | null>(null);

let nextKey = 0;

export function CartProvider({ store, children }: { store: CartStore; children: ReactNode }) {
  const [items, dispatch] = useReducer(reducer, store, (s) => s.load());

  useEffect(() => {
    store.save(items);
  }, [store, items]);

  const api: CartApi = {
    items,
    count: items.reduce((sum, i) => sum + i.quantity, 0),
    add: (choice, quantity) =>
      dispatch({ type: 'add', item: { ...choice, quantity, key: `${Date.now()}-${nextKey++}` } }),
    setQuantity: (key, quantity) => dispatch({ type: 'quantity', key, quantity }),
    remove: (key) => dispatch({ type: 'remove', key }),
    clear: () => dispatch({ type: 'clear' }),
  };
  return <CartContext.Provider value={api}>{children}</CartContext.Provider>;
}

export function useCart(): CartApi {
  const cart = useContext(CartContext);
  if (cart === null) {
    throw new Error('useCart must be used inside <CartProvider>.');
  }
  return cart;
}
