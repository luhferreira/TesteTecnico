import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './playwright/tests',
  fullyParallel: false,         // SQLite em memória — serializa para evitar race conditions
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
  ],
  use: {
    baseURL: process.env.FRONTEND_URL ?? 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // Inicia os serviços antes dos testes quando rodando localmente
  // webServer: [
  //   {
  //     command: 'docker-compose up api web',
  //     url: 'http://localhost:5000/swagger',
  //     reuseExistingServer: true,
  //     timeout: 60_000,
  //   },
  // ],
})
