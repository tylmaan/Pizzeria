import { describe, expect, test } from 'vitest';
import { parsePrice, priceInputValue, toPriceList } from './prices';

describe('prices', () => {
  test.each([
    ['25', 25],
    ['25,5', 25.5],
    ['25.50', 25.5],
    [' 1 299,00 ', 1299],
  ])('parses %j', (input, expected) => {
    expect(parsePrice(input)).toBe(expected);
  });

  test('empty input means "not offered"', () => {
    expect(parsePrice('  ')).toBeNull();
  });

  test.each(['abc', '-5', '12,5,0', '1e3'])('rejects %j', (input) => {
    expect(parsePrice(input)).toBeNaN();
  });

  test('formats stored prices with a decimal comma', () => {
    expect(priceInputValue(25.5)).toBe('25,50');
    expect(priceInputValue('32')).toBe('32,00');
  });

  test('builds the request list in size order, skipping empty sizes and reporting invalid ones', () => {
    expect(toPriceList({ s1: '25,5', s2: '', s3: '40' }, ['s3', 's2', 's1'])).toEqual({
      prices: [
        { sizeId: 's3', price: 40 },
        { sizeId: 's1', price: 25.5 },
      ],
      invalidSizeIds: [],
    });
    expect(toPriceList({ s1: 'x' }, ['s1']).invalidSizeIds).toEqual(['s1']);
  });
});
