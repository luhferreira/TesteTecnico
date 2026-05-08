import { type Page, type Locator } from '@playwright/test'

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

  readonly btnNovaPessoa = this.page.getByRole('button', { name: 'Adicionar Pessoa' })
  readonly inputNome     = this.page.getByLabel('Nome')
  readonly inputDataNasc = this.page.getByLabel(/data de nascimento/i)
  readonly btnSalvar     = this.page.getByRole('button', { name: 'Salvar' })

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async abrirFormNovaPessoa() {
    await this.btnNovaPessoa.click()
    await this.page.waitForTimeout(500)
  }

  async criarPessoa(nome: string, dataNascimento: string) {
    await this.abrirFormNovaPessoa()
    await this.inputNome.fill(nome)
    await this.inputDataNasc.fill(dataNascimento)
    await this.btnSalvar.click()
    await this.waitForIdle()
  }

  pessoaLocator(nome: string) {
    return this.page.getByText(nome)
  }
}

// =============================================================================
// TransacoesPage — /transacoes
// Seletores baseados no snapshot real do Playwright:
//   button "Adicionar Transação"
//   dialog "Adicionar Transação"
//   spinbutton "Valor"
//   combobox "Tipo"
//   textbox "Lista de pessoas"
//   textbox "Lista de categorias"
// =============================================================================
export class TransacoesPage extends BasePage {
  readonly url = '/transacoes'

  readonly btnNovaTransacao = this.page.getByRole('button', { name: 'Adicionar Transação' })
  readonly inputDescricao   = this.page.getByPlaceholder('Digite a descrição')
  readonly inputValor       = this.page.getByRole('spinbutton', { name: 'Valor' })
  readonly selectTipo       = this.page.getByRole('combobox', { name: 'Tipo' })
  readonly inputPessoa      = this.page.getByRole('textbox', { name: 'Lista de pessoas' })
  readonly inputCategoria   = this.page.getByRole('textbox', { name: 'Lista de categorias' })
  readonly btnSalvar        = this.page.getByRole('button', { name: 'Salvar' })
  readonly btnCancelar      = this.page.getByRole('button', { name: 'Cancelar' })
  readonly avisoMenor       = this.page.getByText('Menores só podem registrar despesas.')
  readonly toastSucesso     = this.page.getByText('Transação salva com sucesso!')
  readonly toastErro        = this.page.getByText(/erro ao salvar/i)

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async abrirFormNovaTransacao() {
    await this.btnNovaTransacao.click()
    await this.page.getByRole('dialog', { name: 'Adicionar Transação' })
      .waitFor({ state: 'visible' })
  }

  async selecionarTipo(tipo: 'receita' | 'despesa') {
    await this.selectTipo.selectOption(tipo)
  }

  async selecionarPessoa(nome: string) {
    await this.inputPessoa.click()
    await this.inputPessoa.fill(nome)
    await this.page.waitForTimeout(1000)
    await this.page.locator('[role="option"]').filter({ hasText: nome }).first().click()
  }

  async selecionarCategoria(descricao: string) {
    await this.inputCategoria.click()
    await this.inputCategoria.fill(descricao)
    await this.page.waitForTimeout(1000)
    await this.page.locator('[role="option"]').filter({ hasText: descricao }).first().click()
  }

  async preencherFormulario(params: {
    descricao: string
    valor: string
    tipo: 'receita' | 'despesa'
    pessoaNome?: string
    categoriaNome?: string
  }) {
    await this.inputDescricao.fill(params.descricao)
    await this.inputValor.fill(params.valor)
    await this.selecionarTipo(params.tipo)
    // Preenche a data no formato yyyy-MM-dd (input type="date" padrão HTML)
    const hoje = new Date()
    const dia  = String(hoje.getDate()).padStart(2, '0')
    const mes  = String(hoje.getMonth() + 1).padStart(2, '0')
    const ano  = hoje.getFullYear()
    const dataInput = this.page.getByLabel('Data')
    await dataInput.fill(`${ano}-${mes}-${dia}`)
    if (params.pessoaNome)    await this.selecionarPessoa(params.pessoaNome)
    if (params.categoriaNome) await this.selecionarCategoria(params.categoriaNome)
  }
}

// =============================================================================
// CategoriasPage — /categorias
// =============================================================================
export class CategoriasPage extends BasePage {
  readonly url = '/categorias'

  readonly btnNovaCategoria = this.page.getByRole('button', { name: /adicionar categoria|nova categoria/i })
  readonly inputDescricao   = this.page.getByLabel('Descrição')
  readonly selectFinalidade = this.page.getByLabel('Finalidade')
  readonly btnSalvar        = this.page.getByRole('button', { name: 'Salvar' })

  async goto() {
    await this.page.goto(this.url)
    await this.waitForIdle()
  }

  async criarCategoria(descricao: string, finalidade: 'despesa' | 'receita' | 'ambas') {
    await this.btnNovaCategoria.click()
    await this.page.waitForTimeout(500)
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
}
