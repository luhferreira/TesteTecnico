import { test, expect, ETipo, EFinalidade } from '../fixtures'

test.describe('Fluxo de Pessoas', () => {

  // =========================================================================
  // Navegação
  // =========================================================================

  test('página /pessoas carrega sem erro', async ({ pessoasPage }) => {
    await pessoasPage.goto()
    await expect(pessoasPage.page).not.toHaveTitle(/erro|error/i)
  })

  test('sidebar contém link para Pessoas', async ({ dashboardPage }) => {
    await dashboardPage.goto()
    await expect(dashboardPage.page.getByRole('link', { name: 'Pessoas' })).toBeVisible()
  })

  // =========================================================================
  // Criar Pessoa — verifica via API pois a lista é paginada (8 por página)
  // =========================================================================

  test('cria pessoa adulta e aparece na lista', async ({ pessoasPage, api }) => {
    const nome = `Adulto PW ${Date.now()}`
    await pessoasPage.goto()
    await pessoasPage.criarPessoa(nome, '1990-05-20')

    // A lista é paginada — confirma via API que foi criada
    const resp = await api.get(`/api/v1/pessoas?search=${encodeURIComponent(nome)}`)
    const body = await resp.json()
    const items = body.items ?? body
    expect(items.some((p: { nome: string }) => p.nome === nome)).toBe(true)
  })

  test('cria pessoa menor de idade com sucesso', async ({ pessoasPage, api }) => {
    const hoje    = new Date()
    const nasc    = new Date(hoje.getFullYear() - 10, hoje.getMonth(), hoje.getDate())
    const nascStr = nasc.toISOString().split('T')[0]
    const nome    = `Menor PW ${Date.now()}`

    await pessoasPage.goto()
    await pessoasPage.criarPessoa(nome, nascStr)

    // Confirma via API
    const resp = await api.get(`/api/v1/pessoas?search=${encodeURIComponent(nome)}`)
    const body = await resp.json()
    const items = body.items ?? body
    expect(items.some((p: { nome: string }) => p.nome === nome)).toBe(true)
  })

  test('formulário exibe erro ao submeter com nome vazio', async ({ pessoasPage }) => {
    await pessoasPage.goto()
    await pessoasPage.abrirFormNovaPessoa()
    await pessoasPage.btnSalvar.click()

    const erroNome = pessoasPage.page.getByText(/nome é obrigatório/i)
    await expect(erroNome).toBeVisible({ timeout: 3000 })
  })

  // =========================================================================
  // Exclusão em Cascata — BUG-003
  // =========================================================================

  test('BUG-003 — DELETE pessoa com transações: verifica cascata', async ({
    pessoaAdulta, catDespesa, api,
  }) => {
    const txResp = await api.post('/api/v1/transacoes', {
      data: {
        descricao:   'Despesa cascata E2E',
        valor:       100,
        tipo:        ETipo.Despesa,
        categoriaId: catDespesa.id,
        pessoaId:    pessoaAdulta.id,
        data:        new Date().toISOString(),
      },
    })
    expect(txResp.ok()).toBeTruthy()
    const tx = await txResp.json()

    const delResp = await api.delete(`/api/v1/pessoas/${pessoaAdulta.id}`)
    expect(delResp.status()).toBe(204)

    const txCheck = await api.get(`/api/v1/transacoes/${tx.id}`)
    expect(txCheck.status()).toBe(404)
  })
})
