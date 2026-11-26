import type { components } from './schema';

type MenuDto = components['schemas']['MenuDto'];
type ModifierGroupDto = components['schemas']['ModifierGroupDto'];
type ProductDto = components['schemas']['ProductDto'];

// Price PREVIEW for the shop cart and phone orders. The API prices every order from scratch (Domain ItemPricing) and ignores
// anything computed here, so this only has to be good enough to show the customer a sensible total.

export type ItemChoice = {
  productId: string;
  secondHalfProductId: string | null;
  sizeId: string;
  modifierIds: string[];
};

export const priceAt = (product: ProductDto, sizeId: string): number | null => {
  const price = product.prices.find((p) => p.sizeId === sizeId)?.price;
  return price === undefined ? null : Number(price);
};

/** Lowest price of a product, for "from 25,00 zł" on the menu. */
export const lowestPrice = (product: ProductDto): number => Math.min(...product.prices.map((p) => Number(p.price)));

/** Groups the customer can choose from: the product's own, or those shared by both halves. */
export function allowedGroups(menu: MenuDto, product: ProductDto, secondHalf: ProductDto | null): ModifierGroupDto[] {
  return product.modifierGroupIds
    .filter((id) => secondHalf === null || secondHalf.modifierGroupIds.includes(id))
    .map((id) => menu.modifierGroups.find((g) => g.id === id))
    .filter((g): g is ModifierGroupDto => g !== undefined);
}

export function selectionIsComplete(groups: ModifierGroupDto[], modifierIds: string[]): boolean {
  return groups.every((group) => {
    const count = group.modifiers.filter((m) => modifierIds.includes(m.id)).length;
    return count >= group.minSelect && count <= group.maxSelect;
  });
}

const roundToGrosz = (amount: number) => Math.sign(amount) * Math.round(Math.abs(amount) * 100 + Number.EPSILON) / 100;

/** Price of one piece, or `null` when the choice is not orderable (e.g. a size the product lacks). */
export function previewUnitPrice(menu: MenuDto, choice: ItemChoice): number | null {
  const product = menu.products.find((p) => p.id === choice.productId);
  if (!product) {
    return null;
  }
  const first = priceAt(product, choice.sizeId);
  if (first === null) {
    return null;
  }

  let base = first;
  let secondHalf: ProductDto | null = null;
  if (choice.secondHalfProductId) {
    secondHalf = menu.products.find((p) => p.id === choice.secondHalfProductId) ?? null;
    const second = secondHalf ? priceAt(secondHalf, choice.sizeId) : null;
    if (second === null) {
      return null;
    }
    base = menu.halfAndHalfPricing === 'AverageOfHalves' ? roundToGrosz((first + second) / 2) : Math.max(first, second);
  }

  const modifiers = allowedGroups(menu, product, secondHalf).flatMap((g) => g.modifiers);
  let total = base;
  for (const id of choice.modifierIds) {
    const price = modifiers.find((m) => m.id === id)?.prices.find((p) => p.sizeId === choice.sizeId)?.price;
    if (price === undefined) {
      return null;
    }
    total += Number(price);
  }
  return roundToGrosz(total);
}
