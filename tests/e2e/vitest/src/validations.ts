// =============================================================================
// Espelhos das funções de validação do frontend real
// Baseado em:
//   web/src/lib/schemas.ts  — zod schemas
//   web/src/types/domain.ts — enums TipoTransacao e Finalidade
//   web/src/components/molecules/TransacaoForm.tsx — lógica isMinor
// =============================================================================

/**
 * Espelho de TipoTransacao (domain.ts).
 * Valores lowercase para compatibilidade com a API serializada.
 */
export enum TipoTransacao {
  Despesa = 'despesa',
  Receita = 'receita',
}

/**
 * Espelho de Finalidade (domain.ts).
 */
export enum Finalidade {
  Despesa = 'despesa',
  Receita = 'receita',
  Ambas   = 'ambas',
}

export interface ValidacaoTransacao {
  valido: boolean
  erros: string[]
}

/**
 * Espelho da lógica de validação do TransacaoForm.tsx:
 *   const isMinor = selectedPessoa && selectedPessoa.idade < 18
 *   if (isMinor && data.tipo === TipoTransacao.Receita) → toast.error(...)
 *
 * E das regras do domínio (Transacao.Categoria setter):
 *   categoria.PermiteTipo(tipo)
 */
export function validarTransacao(params: {
  tipo: TipoTransacao
  finalidadeCategoria: Finalidade
  idadePessoa: number
  valor: number
  descricao: string
}): ValidacaoTransacao {
  const erros: string[] = []

  // Regra 1: menor de idade não pode ter receita
  if (params.tipo === TipoTransacao.Receita && params.idadePessoa < 18) {
    erros.push('Menores de 18 anos não podem registrar receitas.')
  }

  // Regra 2: categoria deve ser compatível com o tipo
  const categoriaIncompativel =
    params.finalidadeCategoria !== Finalidade.Ambas &&
    params.finalidadeCategoria !== (params.tipo as unknown as Finalidade)

  if (categoriaIncompativel) {
    erros.push(
      `Categoria do tipo "${params.finalidadeCategoria}" não é compatível com transação "${params.tipo}".`
    )
  }

  // Regra 3: valor deve ser positivo (schema Zod: z.number().positive())
  if (params.valor <= 0) {
    erros.push('Valor deve ser positivo.')
  }

  // Regra 4: descrição obrigatória (schema Zod: z.string().min(1))
  if (!params.descricao || params.descricao.trim().length === 0) {
    erros.push('Descrição é obrigatória.')
  }

  return { valido: erros.length === 0, erros }
}

/**
 * Espelho de Pessoa.CalcularIdade (backend) / idade computada no frontend.
 * O frontend recebe `idade` já calculada pela API (Pessoa.Idade).
 * Esta função é usada para calcular a idade localmente nos testes.
 */
export function calcularIdade(dataNascimento: Date, referencia?: Date): number {
  const ref = referencia ?? new Date()
  let anos  = ref.getFullYear() - dataNascimento.getFullYear()
  const mDiff = ref.getMonth() - dataNascimento.getMonth()
  if (mDiff < 0 || (mDiff === 0 && ref.getDate() < dataNascimento.getDate())) {
    anos--
  }
  return anos
}

/**
 * Verifica compatibilidade de categoria com tipo de transação.
 * Espelha Categoria.PermiteTipo do domínio.
 */
export function categoriaPermiteTipo(
  finalidade: Finalidade,
  tipo: TipoTransacao
): boolean {
  if (finalidade === Finalidade.Ambas) return true
  return finalidade === (tipo as unknown as Finalidade)
}
