import type { PriceDto } from '@pizzeria/api-client';

/** Text of a price input: `25.5` → `25,50` (Polish decimal comma). */
export function priceInputValue(price: number | string | undefined): string {
  return price === undefined ? '' : Number(price).toFixed(2).replace('.', ',');
}

/**
 * Parses what staff type into a price input: accepts `25`, `25,5`, `25.50`, `1 299,00`.
 * Returns `null` for an empty input and `NaN` for anything that is not a number.
 */
export function parsePrice(input: string): number | null {
  const normalized = input.replace(/\s/g, '').replace(',', '.');
  if (normalized === '') {
    return null;
  }
  return /^\d+(\.\d+)?$/.test(normalized) ? Number(normalized) : Number.NaN;
}

export type PriceInputs = Record<string, string>;

export function toPriceInputs(prices: readonly PriceDto[]): PriceInputs {
  return Object.fromEntries(prices.map((p) => [p.sizeId, priceInputValue(p.price)]));
}

/**
 * Builds the request price list in size order, skipping empty inputs (size not offered).
 * Returns the size ids of inputs that are not valid numbers instead, so the form can mark them.
 */
export function toPriceList(
  inputs: PriceInputs,
  sizeIds: readonly string[],
): { prices: PriceDto[]; invalidSizeIds: string[] } {
  const prices: PriceDto[] = [];
  const invalidSizeIds: string[] = [];
  for (const sizeId of sizeIds) {
    const price = parsePrice(inputs[sizeId] ?? '');
    if (price === null) {
      continue;
    }
    if (Number.isNaN(price)) {
      invalidSizeIds.push(sizeId);
    } else {
      prices.push({ sizeId, price });
    }
  }
  return { prices, invalidSizeIds };
}
