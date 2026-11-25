import { buttonClassName } from '@pizzeria/ui';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

export function PageHeader({ title, action }: { title: string; action?: { to: string; label: string } }) {
  return (
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-3xl font-bold">{title}</h1>
      {action && (
        <Link
          to={action.to}
          className={buttonClassName()}
        >
          {action.label}
        </Link>
      )}
    </div>
  );
}

export function QueryState({ children }: { children: ReactNode }) {
  return <p className="text-lg">{children}</p>;
}
