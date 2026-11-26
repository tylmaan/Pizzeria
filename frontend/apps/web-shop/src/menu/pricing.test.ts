import { describe, expect, test } from 'vitest';
import { menu } from '../test/fixtures';
import { allowedGroups, previewUnitPrice, selectionIsComplete } from './pricing';

const margherita = menu.products[0]!;
const pepperoni = menu.products[1]!;

describe('price preview', () => {
  test('base price of the size plus modifiers', () => {
    expect(previewUnitPrice(menu, { productId: 'margherita', secondHalfProductId: null, sizeId: 's42', modifierIds: ['tomato', 'cheese'] })).toBe(41);
  });

  test('half-and-half uses the more expensive half by default', () => {
    expect(previewUnitPrice(menu, { productId: 'margherita', secondHalfProductId: 'pepperoni', sizeId: 's32', modifierIds: ['garlic'] })).toBe(32);
  });

  test('half-and-half average is rounded to grosze', () => {
    const avg = { ...menu, halfAndHalfPricing: 'AverageOfHalves' as const };
    const odd = { ...avg, products: [margherita, { ...pepperoni, prices: [{ sizeId: 's32', price: 25.01 }] }] };
    expect(previewUnitPrice(odd, { productId: 'margherita', secondHalfProductId: 'pepperoni', sizeId: 's32', modifierIds: [] })).toBe(25.01);
  });

  test('only groups shared by both halves are offered', () => {
    expect(allowedGroups(menu, margherita, pepperoni).map((g) => g.id)).toEqual(['sauce']);
    expect(allowedGroups(menu, margherita, null).map((g) => g.id)).toEqual(['sauce', 'toppings']);
  });

  test('required groups must be satisfied', () => {
    const groups = allowedGroups(menu, margherita, null);
    expect(selectionIsComplete(groups, ['cheese'])).toBe(false);
    expect(selectionIsComplete(groups, ['garlic', 'cheese'])).toBe(true);
  });

  test('an unknown modifier makes the item unorderable', () => {
    expect(previewUnitPrice(menu, { productId: 'pepperoni', secondHalfProductId: null, sizeId: 's32', modifierIds: ['cheese'] })).toBeNull();
  });
});
