import { initI18n } from '@pizzeria/i18n';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

initI18n();

// Vitest runs without globals, so Testing Library cannot register its automatic cleanup.
afterEach(() => cleanup());
