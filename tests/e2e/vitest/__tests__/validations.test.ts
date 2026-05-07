import { describe, it, expect } from 'vitest'
import {
  validarTransacao,
  calcularIdade,
  categoriaPermiteTipo,
  TipoTransacao,
  Finalidade,
} from '../src/validations'

// =============================================================================
// calcularIdade
// =============================================================================

describe('calcularIdade', () => {
  it('retorna 34 quando aniversário já ocorreu no ano', () => {
    const nasc = new Date(1990, 2, 10) // 10/03/1990
    const ref  = new Date(2024, 5, 15) // 15/06/2024
    expect(calcularIdade(nasc, ref)).toBe(34)
  })

  it('retorna 33 quando aniversário ainda não ocorreu no ano', () => {
    const nasc = new Date(1990, 11, 25) // 25/12/1990
    const ref  = new Date(2024, 5, 15)  // 15/06/2024
    expect(calcularIdade(nasc, ref)).toBe(33)
  })

  it('retorna 18 no exato dia do 18º aniversário', () => {
    const nasc = new Date(2006, 5, 15)
    const ref  = new Date(2024, 5, 15)
    expect(calcularIdade(nasc, ref)).toBe(18)
  })

  it('retorna 17 na véspera do 18º aniversário (faz 18 amanhã)', () => {
    const nasc = new Date(2006, 5, 16) // faz 18 anos em 16/06/2024
    const ref  = new Date(2024, 5, 15) // hoje é 15/06/2024
    expect(calcularIdade(nasc, ref)).toBe(17)
  })

  it('retorna 0 para recém-nascido no mesmo dia', () => {
    const hoje = new Date()
    expect(calcularIdade(hoje, hoje)).toBe(0)
  })
})

// =============================================================================
// categoriaPermiteTipo — espelha Categoria.PermiteTipo do domínio
// =============================================================================

describe('categoriaPermiteTipo', () => {
  it.each<[Finalidade, TipoTransacao, boolean]>([
    [Finalidade.Receita, TipoTransacao.Receita, true],
    [Finalidade.Receita, TipoTransacao.Despesa, false],
    [Finalidade.Despesa, TipoTransacao.Despesa, true],
    [Finalidade.Despesa, TipoTransacao.Receita, false],
    [Finalidade.Ambas,   TipoTransacao.Receita, true],
    [Finalidade.Ambas,   TipoTransacao.Despesa, true],
  ])(
    'finalidade=%s + tipo=%s → permite=%s',
    (finalidade, tipo, esperado) => {
      expect(categoriaPermiteTipo(finalidade, tipo)).toBe(esperado)
    }
  )
})

// =============================================================================
// validarTransacao — Regra 1: menor de idade não pode ter Receita
// Espelha lógica do TransacaoForm.tsx
// =============================================================================

describe('validarTransacao — menor de idade não pode ter Receita', () => {
  it('menor com 17 anos tentando criar Receita gera erro', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Receita,
      idadePessoa:         17,
      valor:               100,
      descricao:           'Mesada',
    })
    expect(result.valido).toBe(false)
    expect(result.erros).toContain('Menores de 18 anos não podem registrar receitas.')
  })

  it('menor com 0 anos tentando criar Receita gera erro', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Receita,
      idadePessoa:         0,
      valor:               100,
      descricao:           'Bebê rico',
    })
    expect(result.valido).toBe(false)
  })

  it('menor com 17 anos pode criar Despesa', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Despesa,
      finalidadeCategoria: Finalidade.Despesa,
      idadePessoa:         17,
      valor:               15,
      descricao:           'Lanche',
    })
    expect(result.valido).toBe(true)
    expect(result.erros).toHaveLength(0)
  })

  it('adulto com 18 anos pode criar Receita', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Receita,
      idadePessoa:         18,
      valor:               3000,
      descricao:           'Salário',
    })
    expect(result.valido).toBe(true)
  })

  it('limite exato: 17 anos e 364 dias ainda é menor (não pode ter Receita)', () => {
    // Calcula a idade de alguém que faz 18 anos amanhã
    const amanha     = new Date()
    amanha.setDate(amanha.getDate() + 1)
    const nascimento = new Date(amanha)
    nascimento.setFullYear(nascimento.getFullYear() - 18)
    const idade      = calcularIdade(nascimento) // deve ser 17

    expect(idade).toBe(17)

    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Receita,
      idadePessoa:         idade,
      valor:               200,
      descricao:           'Bico',
    })
    expect(result.valido).toBe(false)
  })
})

// =============================================================================
// validarTransacao — Regra 2: compatibilidade de categoria
// =============================================================================

describe('validarTransacao — compatibilidade de categoria', () => {
  it('Finalidade.Receita + TipoTransacao.Despesa gera erro', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Despesa,
      finalidadeCategoria: Finalidade.Receita,
      idadePessoa:         30,
      valor:               150,
      descricao:           'Conta de luz',
    })
    expect(result.valido).toBe(false)
    expect(result.erros.some(e => e.includes('receita') && e.includes('despesa'))).toBe(true)
  })

  it('Finalidade.Despesa + TipoTransacao.Receita gera erro', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Despesa,
      idadePessoa:         25,
      valor:               500,
      descricao:           'Freelance',
    })
    expect(result.valido).toBe(false)
    expect(result.erros.some(e => e.includes('despesa') && e.includes('receita'))).toBe(true)
  })

  it.each<TipoTransacao>([TipoTransacao.Receita, TipoTransacao.Despesa])(
    'Finalidade.Ambas aceita tipo=%s sem erro',
    (tipo) => {
      const result = validarTransacao({
        tipo,
        finalidadeCategoria: Finalidade.Ambas,
        idadePessoa:         25,
        valor:               200,
        descricao:           'Transferência',
      })
      expect(result.valido).toBe(true)
    }
  )
})

// =============================================================================
// validarTransacao — Regras básicas de DTO (Zod schema)
// =============================================================================

describe('validarTransacao — validações básicas de campo', () => {
  it('valor zero gera erro', () => {
    const result = validarTransacao({
      tipo: TipoTransacao.Despesa, finalidadeCategoria: Finalidade.Despesa,
      idadePessoa: 30, valor: 0, descricao: 'Teste',
    })
    expect(result.valido).toBe(false)
    expect(result.erros.some(e => e.toLowerCase().includes('valor'))).toBe(true)
  })

  it('valor negativo gera erro', () => {
    const result = validarTransacao({
      tipo: TipoTransacao.Despesa, finalidadeCategoria: Finalidade.Despesa,
      idadePessoa: 30, valor: -100, descricao: 'Teste',
    })
    expect(result.valido).toBe(false)
  })

  it('descrição vazia gera erro', () => {
    const result = validarTransacao({
      tipo: TipoTransacao.Despesa, finalidadeCategoria: Finalidade.Despesa,
      idadePessoa: 30, valor: 100, descricao: '',
    })
    expect(result.valido).toBe(false)
    expect(result.erros.some(e => e.toLowerCase().includes('descrição'))).toBe(true)
  })

  it('descrição apenas com espaços gera erro', () => {
    const result = validarTransacao({
      tipo: TipoTransacao.Despesa, finalidadeCategoria: Finalidade.Despesa,
      idadePessoa: 30, valor: 100, descricao: '   ',
    })
    expect(result.valido).toBe(false)
  })

  it('múltiplas violações acumulam todos os erros', () => {
    const result = validarTransacao({
      tipo:                TipoTransacao.Receita,
      finalidadeCategoria: Finalidade.Despesa,  // incompatível
      idadePessoa:         16,                   // menor
      valor:               -10,                  // inválido
      descricao:           '',                   // vazio
    })
    expect(result.valido).toBe(false)
    expect(result.erros.length).toBeGreaterThanOrEqualTo(3)
  })
})
