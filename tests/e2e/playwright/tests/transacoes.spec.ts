import { test, expect, ETipo, EFinalidade } from '../fixtures'

/**
 * Testes E2E — Regras de negócio de Transações na UI.
 *
 * Baseado no código real:
 *   - TransacaoForm.tsx: isMinor check + toast.error para menor com receita
 *   - TipoSelect.tsx: <select id="tipo"> com option[value="receita"] disabled={disableReceita}
 *   - LazySelect.tsx: input com placeholder + role=option no dropdown
 */

test.describe('Regras de negócio — Transações (UI)', () => {

  // =========================================================================
  // Menor de idade — aviso visual e bloqueio de Receita
  // =========================================================================

  test.describe('Menor de idade', () => {

    test('ao selecionar menor, exibe aviso "Menores só podem registrar despesas."', async ({
      transacoesPage, pessoaMenor,
    }) => {
      await transacoesPage.goto()
      await transacoesPage.abrirFormNovaTransacao()
      await transacoesPage.selecionarPessoa(pessoaMenor.nome)

      // TransacaoForm.tsx linha: {isMinor && <p>Menores só podem registrar despesas.</p>}
      await expect(transacoesPage.avisoMenor).toBeVisible({ timeout: 5000 })
    })

    test('ao selecionar menor, opção Receita fica desabilitada no TipoSelect', async ({
      transacoesPage, pessoaMenor,
    }) => {
      await transacoesPage.goto()
      await transacoesPage.abrirFormNovaTransacao()
      await transacoesPage.selecionarPessoa(pessoaMenor.nome)

      // TipoSelect.tsx: <option value="receita" disabled={disableReceita}>
      const optionReceita = transacoesPage.page.locator('select#tipo option[value="receita"]')
      await expect(optionReceita).toBeDisabled({ timeout: 3000 })
    })

    test('menor de idade pode criar Despesa sem erro', async ({
      transacoesPage, pessoaMenor, catDespesa,
    }) => {
      await transacoesPage.goto()
      await transacoesPage.abrirFormNovaTransacao()

      await transacoesPage.preencherFormulario({
        descricao:     'Lanche E2E',
        valor:         '15',
        tipo:          'despesa',
        pessoaNome:    pessoaMenor.nome,
        categoriaNome: catDespesa.descricao,
      })
      await transacoesPage.btnSalvar.click()
      await transacoesPage.waitForIdle()

      // Toast de sucesso deve aparecer
      await expect(transacoesPage.toastSucesso).toBeVisible({ timeout: 5000 })
    })
  })

  // =========================================================================
  // Happy Path
  // =========================================================================

  test('adulto + categoria Receita + tipo Receita → cria com sucesso', async ({
    transacoesPage, pessoaAdulta, catReceita,
  }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()

    await transacoesPage.preencherFormulario({
      descricao:     `Salário E2E ${Date.now()}`,
      valor:         '5000',
      tipo:          'receita',
      pessoaNome:    pessoaAdulta.nome,
      categoriaNome: catReceita.descricao,
    })
    await transacoesPage.btnSalvar.click()

    await expect(transacoesPage.toastSucesso).toBeVisible({ timeout: 5000 })
  })

  test('adulto + categoria Ambas + tipo Receita → cria com sucesso', async ({
    transacoesPage, pessoaAdulta, catAmbas,
  }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()

    await transacoesPage.preencherFormulario({
      descricao:     `Transferência E2E ${Date.now()}`,
      valor:         '500',
      tipo:          'receita',
      pessoaNome:    pessoaAdulta.nome,
      categoriaNome: catAmbas.descricao,
    })
    await transacoesPage.btnSalvar.click()

    await expect(transacoesPage.toastSucesso).toBeVisible({ timeout: 5000 })
  })

  // =========================================================================
  // Validação de campos obrigatórios (Zod schema)
  // =========================================================================

  test('formulário exibe erro ao submeter sem descrição', async ({ transacoesPage }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()
    await transacoesPage.btnSalvar.click()

    // Zod schema: descricao min(1)
    await expect(
      transacoesPage.page.getByText(/descrição.*obrigatória|obrigatório/i)
    ).toBeVisible({ timeout: 3000 })
  })

  test('formulário exibe erro ao submeter valor não positivo', async ({ transacoesPage }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()
    await transacoesPage.inputDescricao.fill('Teste')
    await transacoesPage.inputValor.fill('0')
    await transacoesPage.btnSalvar.click()

    // Zod schema: valor positive()
    await expect(
      transacoesPage.page.getByText(/valor.*positivo|maior.*zero/i)
    ).toBeVisible({ timeout: 3000 })
  })
})

// =============================================================================
// Testes de API direta — documenta BUGs de status HTTP
// =============================================================================

test.describe('BUGs de status HTTP na API', () => {

  test('BUG-001 — API retorna 500 ao criar Receita para menor (deveria ser 400/422)', async ({
    api, pessoaMenor, catReceita,
  }) => {
    const resp = await api.post('/api/v1/transacoes', {
      data: {
        descricao:   'Mesada indevida',
        valor:       100,
        tipo:        ETipo.Receita,
        categoriaId: catReceita.id,
        pessoaId:    pessoaMenor.id,
        data:        new Date().toISOString(),
      },
    })
    expect(resp.status()).toBe(500)
  })

  test('BUG-002 — API retorna 500 ao usar categoria Receita em Despesa (deveria ser 400/422)', async ({
    api, pessoaAdulta, catReceita,
  }) => {
    const resp = await api.post('/api/v1/transacoes', {
      data: {
        descricao:   'Conta de luz indevida',
        valor:       150,
        tipo:        ETipo.Despesa,
        categoriaId: catReceita.id,
        pessoaId:    pessoaAdulta.id,
        data:        new Date().toISOString(),
      },
    })
    expect(resp.status()).toBe(500)
  })
})
