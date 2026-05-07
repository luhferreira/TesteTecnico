import { type Page, type Locator, expect } from '@playwright/test'

// =============================================================================
// Baseados em:
//   web/src/lib/routes.ts — paths: /, /transacoes, /categorias, /pessoas, /totais
//   web/src/components/molecules/TransacaoForm.tsx — campos reais
//   web/src/components/molecules/PessoaForm.tsx — campos reais
//   web/src/components/molecules/CategoriaForm.tsx — campos reais
//   web/src/types/domain.ts — TipoTransacao, Finalidade
// =============================================================================

export abstract class BasePage {
  constructor(protected readonly page: Page) {}
  async waitForIdle() {
    await this.page.waitForLoadState('networkidle')
  }
}

// =============================================================================
// PessoasPage — /pessoas
// =============================================================================
export class PessoasPage extends BasePage {
  readonly url = '/pessoas'

  // PessoaForm usa react-hook-form com labels "Nome" e "Data de Nascimento"
  readonly btnNovaPessoa    = this.page.getByRole('button', { name: /nova pessoa|adicionar/i })
  readonly inputNome        = this.page.getByLabel('Nome')
  readonly inputDataNasc    = this.page.getByLabel('Data de Nascimento')
  // Botão "Salvar" no PessoaForm
  readonly btnSalvar        = this.page.getByRole('button', { name: /salvar/i })
  readonly btnCancelar      = this.page.getByRole('button', { name: /cancelar/i })

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async abrirFormNovaPessoa() {
    await this.btnNovaPessoa.click()
    await this.page.waitForSelector('input', { state: 'visible' })
  }

  async criarPessoa(nome: string, dataNascimento: string) {
    await this.abrirFormNovaPessoa()
    await this.inputNome.fill(nome)
    await this.inputDataNasc.fill(dataNascimento)
    await this.btnSalvar.click()
    await this.waitForIdle()
  }

  async pessoaEstaVisivel(nome: string) {
    return this.page.getByText(nome).isVisible()
  }

  pessoaLocator(nome: string) {
    return this.page.getByText(nome)
  }
}

// =============================================================================
// TransacoesPage — /transacoes
// =============================================================================
export class TransacoesPage extends BasePage {
  readonly url = '/transacoes'

  readonly btnNovaTransacao = this.page.getByRole('button', { name: /nova transação|adicionar/i })
  readonly inputDescricao   = this.page.getByLabel('Descrição')
  readonly inputValor       = this.page.getByLabel('Valor')
  // DateInput usa label "Data"
  readonly inputData        = this.page.getByLabel('Data')
  // TipoSelect — label "Tipo"
  readonly selectTipo       = this.page.getByLabel('Tipo')
  readonly btnSalvar        = this.page.getByRole('button', { name: /salvar/i })

  // Mensagens de validação (react-hot-toast ou alerts)
  readonly toastErro        = this.page.locator('[data-hot-toast]').or(
    this.page.getByRole('alert')
  )
  // Aviso de menor de idade no TransacaoForm
  readonly avisoMenor       = this.page.getByText(/menores só podem registrar despesas/i)

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async abrirFormNovaTransacao() {
    await this.btnNovaTransacao.click()
    await this.page.waitForSelector('form', { state: 'visible' })
  }

  async selecionarPessoa(nome: string) {
    // LazyPessoaSelect é um combobox/select customizado
    const pessoaSelect = this.page.getByPlaceholder(/pessoa/i)
      .or(this.page.getByLabel(/pessoa/i))
    await pessoaSelect.click()
    await this.page.getByText(nome, { exact: true }).click()
  }

  async selecionarCategoria(descricao: string) {
    const catSelect = this.page.getByPlaceholder(/categoria/i)
      .or(this.page.getByLabel(/categoria/i))
    await catSelect.click()
    await this.page.getByText(descricao, { exact: true }).click()
  }

  async preencherFormulario(params: {
    descricao: string
    valor: string
    tipo: 'receita' | 'despesa'
    data?: string
    pessoaNome?: string
    categoriaNome?: string
  }) {
    await this.inputDescricao.fill(params.descricao)
    await this.inputValor.fill(params.valor)
    await this.selectTipo.selectOption(params.tipo)
    if (params.data) await this.inputData.fill(params.data)
    if (params.pessoaNome) await this.selecionarPessoa(params.pessoaNome)
    if (params.categoriaNome) await this.selecionarCategoria(params.categoriaNome)
  }
}

// =============================================================================
// CategoriasPage — /categorias
// =============================================================================
export class CategoriasPage extends BasePage {
  readonly url = '/categorias'

  readonly btnNovaCategoria = this.page.getByRole('button', { name: /nova categoria|adicionar/i })
  readonly inputDescricao   = this.page.getByLabel('Descrição')
  // FinalidadeSelect usa label "Finalidade"
  readonly selectFinalidade = this.page.getByLabel('Finalidade')
  readonly btnSalvar        = this.page.getByRole('button', { name: /salvar/i })

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async criarCategoria(descricao: string, finalidade: 'despesa' | 'receita' | 'ambas') {
    await this.btnNovaCategoria.click()
    await this.page.waitForSelector('form', { state: 'visible' })
    await this.inputDescricao.fill(descricao)
    await this.selectFinalidade.selectOption(finalidade)
    await this.btnSalvar.click()
    await this.waitForIdle()
  }
}

// =============================================================================
// DashboardPage — /
// =============================================================================
export class DashboardPage extends BasePage {
  readonly url = '/'

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async navegarPara(rotulo: 'Transações' | 'Categorias' | 'Pessoas' | 'Relatórios') {
    await this.page.getByRole('link', { name: rotulo }).click()
    await this.waitForIdle()
  }
}
