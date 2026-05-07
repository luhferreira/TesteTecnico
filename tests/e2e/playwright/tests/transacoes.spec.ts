import { test, expect, ETipo, EFinalidade } from '../fixtures'

/**
 * Testes E2E — Regras de negócio de Transações na UI.
 *
 * O TransacaoForm.tsx implementa:
 *   - Verificação de isMinor (idade < 18) → desabilita Receita + aviso visual
 *   - Validação no onSubmit antes de chamar a API
 *
 * BUG-001: Caso o frontend envie Receita para menor à API, esta retorna 500.
 * BUG-002: Caso o frontend envie categoria incompatível, a API retorna 500.
 *
 * Estes testes verificam se a UI previne o envio antes de atingir a API.
 */

test.describe('Regras de negócio — Transações (UI)', () => {

  // =========================================================================
  // Menor de idade — aviso visual e bloqueio de Receita
  // =========================================================================

  test.describe('Menor de idade', () => {

    test('ao selecionar menor, exibe aviso "Menores só podem registrar despesas"', async ({
      transacoesPage, pessoaMenor,
    }) => {
      await transacoesPage.goto()
      await transacoesPage.abrirFormNovaTransacao()
      await transacoesPage.selecionarPessoa(pessoaMenor.nome)

      // TransacaoForm exibe: "Menores só podem registrar despesas."
      await expect(transacoesPage.avisoMenor).toBeVisible({ timeout: 3000 })
    })

    test('ao selecionar menor, opção Receita fica desabilitada no TipoSelect', async ({
      transacoesPage, pessoaMenor,
    }) => {
      await transacoesPage.goto()
      await transacoesPage.abrirFormNovaTransacao()
      await transacoesPage.selecionarPessoa(pessoaMenor.nome)

      // TipoSelect recebe disableReceita={!!isMinor} → option de receita deve estar disabled
      const optionReceita = transacoesPage.page.locator('select[name="tipo"] option[value="receita"]')
        .or(transacoesPage.page.getByRole('option', { name: /receita/i }))

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
        data:          new Date().toISOString().split('T')[0],
        pessoaNome:    pessoaMenor.nome,
        categoriaNome: catDespesa.descricao,
      })
      await transacoesPage.btnSalvar.click()
      await transacoesPage.waitForIdle()

      // Não deve aparecer toast de erro
      await expect(
        transacoesPage.page.getByText(/erro ao salvar/i)
      ).not.toBeVisible({ timeout: 3000 })
    })
  })

  // =========================================================================
  // Happy Path — adulto com categoria compatível
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
      data:          new Date().toISOString().split('T')[0],
      pessoaNome:    pessoaAdulta.nome,
      categoriaNome: catReceita.descricao,
    })
    await transacoesPage.btnSalvar.click()
    await transacoesPage.waitForIdle()

    await expect(
      transacoesPage.page.getByText(/transação salva com sucesso/i)
    ).toBeVisible({ timeout: 5000 })
  })

  test('adulto + categoria Ambas + tipo Receita → cria com sucesso', async ({
    transacoesPage, pessoaAdulta, catAmbas,
  }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()

    await transacoesPage.preencherFormulario({
      descricao:     `Transferência entrada ${Date.now()}`,
      valor:         '500',
      tipo:          'receita',
      data:          new Date().toISOString().split('T')[0],
      pessoaNome:    pessoaAdulta.nome,
      categoriaNome: catAmbas.descricao,
    })
    await transacoesPage.btnSalvar.click()
    await transacoesPage.waitForIdle()

    await expect(
      transacoesPage.page.getByText(/transação salva com sucesso/i)
    ).toBeVisible({ timeout: 5000 })
  })

  // =========================================================================
  // Validação de campos obrigatórios (Zod schema no frontend)
  // =========================================================================

  test('formulário exibe erro ao submeter sem descrição', async ({ transacoesPage }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()
    await transacoesPage.btnSalvar.click()

    await expect(
      transacoesPage.page.getByText(/descrição é obrigatória/i)
    ).toBeVisible({ timeout: 3000 })
  })

  test('formulário exibe erro ao submeter valor não positivo', async ({ transacoesPage }) => {
    await transacoesPage.goto()
    await transacoesPage.abrirFormNovaTransacao()
    await transacoesPage.inputDescricao.fill('Teste')
    await transacoesPage.inputValor.fill('0')
    await transacoesPage.btnSalvar.click()

    await expect(
      transacoesPage.page.getByText(/valor deve ser positivo/i)
    ).toBeVisible({ timeout: 3000 })
  })
})

// =============================================================================
// Testes de API direta — documenta BUGs de HTTP status
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
    // BUG: InvalidOperationException não tratada → 500
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
