import { test, expect, ETipo, EFinalidade } from '../fixtures'

/**
 * Testes E2E — Pessoas.
 * Pré-requisito: docker-compose up -d
 * Execute: cd tests/e2e && bun run test:e2e
 */

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
  // Criar Pessoa
  // =========================================================================

  test('cria pessoa adulta e aparece na lista', async ({ pessoasPage }) => {
    const nome = `Adulto PW ${Date.now()}`
    await pessoasPage.goto()
    await pessoasPage.criarPessoa(nome, '1990-05-20')

    await expect(pessoasPage.pessoaLocator(nome)).toBeVisible({ timeout: 5000 })
  })

  test('cria pessoa menor de idade com sucesso', async ({ pessoasPage }) => {
    const hoje   = new Date()
    const nasc   = new Date(hoje.getFullYear() - 10, hoje.getMonth(), hoje.getDate())
    const nascStr = nasc.toISOString().split('T')[0]
    const nome    = `Menor PW ${Date.now()}`

    await pessoasPage.goto()
    await pessoasPage.criarPessoa(nome, nascStr)

    await expect(pessoasPage.pessoaLocator(nome)).toBeVisible({ timeout: 5000 })
  })

  test('formulário exibe erro ao submeter com nome vazio', async ({ pessoasPage }) => {
    await pessoasPage.goto()
    await pessoasPage.abrirFormNovaPessoa()
    // Não preenche nada, tenta salvar
    await pessoasPage.btnSalvar.click()

    // Zod/react-hook-form deve exibir erro de validação
    const erroNome = pessoasPage.page.getByText(/nome é obrigatório/i)
    await expect(erroNome).toBeVisible({ timeout: 3000 })
  })

  // =========================================================================
  // Exclusão em Cascata — BUG-003
  // =========================================================================

  test('BUG-003 — DELETE pessoa com transações: verifica cascata', async ({
    pessoaAdulta, catDespesa, api,
  }) => {
    // Cria transação via API
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

    // Exclui a pessoa
    const delResp = await api.delete(`/api/v1/pessoas/${pessoaAdulta.id}`)

    // BUG-003: se não houver cascade no EF, delResp pode ser 500 (erro FK)
    expect(delResp.status()).toBe(204)

    // A transação deve ter sido excluída junto (cascade)
    const txCheck = await api.get(`/api/v1/transacoes/${tx.id}`)
    expect(txCheck.status()).toBe(404)
  })
})
