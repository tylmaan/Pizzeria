import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger';

const variantClasses: Record<Variant, string> = {
  primary: 'bg-orange-600 text-white hover:bg-orange-700',
  secondary: 'bg-neutral-200 text-neutral-900 hover:bg-neutral-300',
  danger: 'bg-red-700 text-white hover:bg-red-800',
};

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
};

/** Large touch target (min 48 px) — staff often work with gloves or floury hands. */
export function Button({ variant = 'primary', className = '', type = 'button', ...props }: ButtonProps) {
  return (
    <button
      type={type}
      className={`min-h-12 min-w-12 rounded-lg px-5 text-lg font-semibold disabled:opacity-50 ${variantClasses[variant]} ${className}`}
      {...props}
    />
  );
}
