import { test as base, type APIRequestContext } from '@playwright/test'
import { PessoasPage, TransacoesPage, CategoriasPage, DashboardPage } from '../page-objects'

// =============================================================================
// Enums da API (espelho de Transacao.ETipo e Categoria.EFinalidade)
// =============================================================================
const ETipo      = { Despesa: 0, Receita: 1 } as const
const EFinalidade = { Despesa: 0, Receita: 1, Ambas: 2 } as const

const API_URL      = process.env.API_URL      ?? 'http://localhost:5000'
const FRONTEND_URL = process.env.FRONTEND_URL ?? 'http://localhost:5173'

type AppFixtures = {
  pessoasPage:     PessoasPage
  transacoesPage:  TransacoesPage
  categoriasPage:  CategoriasPage
  dashboardPage:   DashboardPage
  api:             APIRequestContext
  // Entidades pré-criadas via API
  pessoaAdulta:    { id: string; nome: string; idade: number }
  pessoaMenor:     { id: string; nome: string; idade: number }
  catReceita:      { id: string; descricao: string }
  catDespesa:      { id: string; descricao: string }
  catAmbas:        { id: string; descricao: string }
}

export const test = base.extend<AppFixtures>({
  pessoasPage:    async ({ page }, use) => use(new PessoasPage(page)),
  transacoesPage: async ({ page }, use) => use(new TransacoesPage(page)),
  categoriasPage: async ({ page }, use) => use(new CategoriasPage(page)),
  dashboardPage:  async ({ page }, use) => use(new DashboardPage(page)),

  api: async ({ playwright }, use) => {
    const ctx = await playwright.request.newContext({ baseURL: API_URL })
    await use(ctx)
    await ctx.dispose()
  },

  pessoaAdulta: async ({ api }, use) => {
    const resp = await api.post('/api/v1/pessoas', {
      data: { nome: `Adulto E2E ${Date.now()}`, dataNascimento: '1985-06-15T00:00:00' },
    })
    const p = await resp.json()
    await use({ id: p.id, nome: p.nome, idade: p.idade })
    await api.delete(`/api/v1/pessoas/${p.id}`).catch(() => {})
  },

  pessoaMenor: async ({ api }, use) => {
    const nasc = new Date()
    nasc.setFullYear(nasc.getFullYear() - 10)
    const resp = await api.post('/api/v1/pessoas', {
      data: { nome: `Menor E2E ${Date.now()}`, dataNascimento: nasc.toISOString() },
    })
    const p = await resp.json()
    await use({ id: p.id, nome: p.nome, idade: p.idade })
    await api.delete(`/api/v1/pessoas/${p.id}`).catch(() => {})
  },

  catReceita: async ({ api }, use) => {
    const resp = await api.post('/api/v1/categorias', {
      data: { descricao: `Salário E2E ${Date.now()}`, finalidade: EFinalidade.Receita },
    })
    const c = await resp.json()
    await use({ id: c.id, descricao: c.descricao })
    await api.delete(`/api/v1/categorias/${c.id}`).catch(() => {})
  },

  catDespesa: async ({ api }, use) => {
    const resp = await api.post('/api/v1/categorias', {
      data: { descricao: `Mercado E2E ${Date.now()}`, finalidade: EFinalidade.Despesa },
    })
    const c = await resp.json()
    await use({ id: c.id, descricao: c.descricao })
    await api.delete(`/api/v1/categorias/${c.id}`).catch(() => {})
  },

  catAmbas: async ({ api }, use) => {
    const resp = await api.post('/api/v1/categorias', {
      data: { descricao: `Transferência E2E ${Date.now()}`, finalidade: EFinalidade.Ambas },
    })
    const c = await resp.json()
    await use({ id: c.id, descricao: c.descricao })
    await api.delete(`/api/v1/categorias/${c.id}`).catch(() => {})
  },
})

export { expect } from '@playwright/test'
export { ETipo, EFinalidade }
