import { ApiError, errorCodeOf } from '@pizzeria/api-client';
import { useCodeTranslations } from '@pizzeria/i18n';

/** Turns a failed mutation into a banner message plus per-field messages (keyed by API field path). */
export function useFormErrors(error: unknown) {
  const codes = useCodeTranslations();
  const fieldErrors = error instanceof ApiError ? error.fieldErrors : {};

  return {
    general: error ? codes.error(errorCodeOf(error)) : undefined,
    field: (path: string): string | undefined => {
      const [first] = fieldErrors[path] ?? [];
      return first ? codes.validation(first) : undefined;
    },
  };
}
