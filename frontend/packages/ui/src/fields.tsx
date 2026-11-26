import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react';

// Components receive already-translated texts: this package holds no UI strings of its own.

const controlClasses =
  'min-h-12 w-full rounded-lg border-2 bg-white px-3 text-lg text-neutral-900 outline-none focus:border-orange-600';

type FieldProps = {
  label: string;
  /** Translated validation message shown under the control. */
  error?: string | undefined;
  hint?: string | undefined;
};

function FieldShell({ id, label, error, hint, children }: FieldProps & { id: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-base font-semibold">
        {label}
      </label>
      {children}
      {hint && !error && <p className="text-sm text-neutral-600">{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm font-semibold text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}

export type TextFieldProps = FieldProps & Omit<InputHTMLAttributes<HTMLInputElement>, 'id'>;

export function TextField({ label, error, hint, className = '', ...props }: TextFieldProps) {
  const id = useId();
  return (
    <FieldShell id={id} label={label} error={error} hint={hint}>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        className={`${controlClasses} ${error ? 'border-red-700' : 'border-neutral-400'} ${className}`}
        {...props}
      />
    </FieldShell>
  );
}

export type SelectFieldProps = FieldProps & Omit<SelectHTMLAttributes<HTMLSelectElement>, 'id'>;

export function SelectField({ label, error, hint, className = '', children, ...props }: SelectFieldProps) {
  const id = useId();
  return (
    <FieldShell id={id} label={label} error={error} hint={hint}>
      <select
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        className={`${controlClasses} ${error ? 'border-red-700' : 'border-neutral-400'} ${className}`}
        {...props}
      >
        {children}
      </select>
    </FieldShell>
  );
}

export type CheckboxProps = { label: string } & Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'type'>;

/** Whole row is the touch target (min 48 px), not just the small box. */
export function Checkbox({ label, className = '', ...props }: CheckboxProps) {
  const id = useId();
  return (
    <label
      htmlFor={id}
      className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-lg px-2 text-lg hover:bg-neutral-200 ${className}`}
    >
      <input id={id} type="checkbox" className="size-6 accent-orange-600" {...props} />
      {label}
    </label>
  );
}

const alertTones = {
  error: 'border-red-700 bg-red-50 font-semibold text-red-800',
  success: 'border-green-700 bg-green-50 text-green-900',
  info: 'border-neutral-400 bg-neutral-50 text-neutral-900',
} as const;

/** Errors interrupt screen readers (role=alert); success and info messages are announced politely. */
export function Alert({ children, tone = 'error' }: { children: ReactNode; tone?: keyof typeof alertTones }) {
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-lg border-2 px-4 py-3 text-lg ${alertTones[tone]}`}>
      {children}
    </div>
  );
}
