import { defineConfig } from 'vitest/config'

export default defineConfig({
  // keepNames mirrors the tsx transform the CLI runs under, so tests catch code that breaks inside page.evaluate.
  esbuild: { keepNames: true },
  test: { include: ['test/**/*.test.ts'], testTimeout: 60_000, hookTimeout: 60_000, fileParallelism: false },
})
