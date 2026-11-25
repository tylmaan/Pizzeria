import type { ReactNode } from 'react';
import { Link } from 'react-router';

export function PageHeader({ title, action }: { title: string; action?: { to: string; label: string } }) {
  return (
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-3xl font-bold">{title}</h1>
      {action && (
        <Link
          to={action.to}
          className="flex min-h-12 items-center rounded-lg bg-orange-600 px-5 text-lg font-semibold text-white hover:bg-orange-700"
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
