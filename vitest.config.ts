import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
export default defineConfig({ test: { include: ['apps/api/test/**/*.test.ts', 'apps/api/test/**/*.e2e-spec.ts', 'apps/extension/test/**/*.test.ts', 'packages/**/*.test.ts'], testTimeout: 30000, hookTimeout: 120000, fileParallelism: false }, resolve: { alias: { '@contextspeak/contracts': fileURLToPath(new URL('./packages/contracts/src/lookup.ts', import.meta.url)) } } });
