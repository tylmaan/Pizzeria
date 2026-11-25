import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger';

const variantClasses: Record<Variant, string> = {
  primary: 'bg-orange-600 text-white hover:bg-orange-700',
  secondary: 'bg-neutral-200 text-neutral-900 hover:bg-neutral-300',
  danger: 'bg-red-700 text-white hover:bg-red-800',
};

/**
 * Large touch target (min 48 px) — staff often work with gloves or floury hands.
 * Exported so router links can look like buttons without this package depending on the router.
 */
export function buttonClassName(variant: Variant = 'primary', className = ''): string {
  return `inline-flex min-h-12 min-w-12 items-center justify-center rounded-lg px-5 text-lg font-semibold disabled:opacity-50 ${variantClasses[variant]} ${className}`;
}

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
};

export function Button({ variant = 'primary', className = '', type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={buttonClassName(variant, className)} {...props} />;
}
