# BUG-002 — Controller retorna 500 para categoria incompatível (deveria ser 400/422)

## Status
🔴 **Confirmado** — mesma causa raiz do BUG-001

## Descrição

A regra de compatibilidade está implementada no setter interno `Transacao.Categoria`:

```csharp
// MinhasFinancas.Domain/Entities/Transacao.cs
public Categoria? Categoria
{
    internal set
    {
        _categoria = value;
        if (value != null)
        {
            CategoriaId = value.Id;
            if (!value.PermiteTipo(Tipo))
            {
                throw new InvalidOperationException(
                    Tipo == ETipo.Despesa
                        ? "Não é possível registrar despesa em categoria de receita."
                        : "Não é possível registrar receita em categoria de despesa.");
            }
        }
    }
}
```

O `TransacaoService` atribui `Categoria` **antes** de `Pessoa`:

```csharp
// TransacaoService.cs — CreateAsync
var transacao = new Transacao
{
    ...
    Categoria = categoria,   // ← lança InvalidOperationException se incompatível
    Pessoa    = pessoa
};
```

O controller captura apenas `ArgumentException` → `InvalidOperationException` → 500.

## Reprodução

```bash
# 1. Cria adulto
PESSOA_ID=$(curl -s -X POST http://localhost:5000/api/v1/pessoas \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Adulto Bug002","dataNascimento":"1990-01-01T00:00:00"}' | jq -r .id)

# 2. Cria categoria RECEITA (finalidade=1)
CAT_ID=$(curl -s -X POST http://localhost:5000/api/v1/categorias \
  -H 'Content-Type: application/json' \
  -d '{"descricao":"Salário Bug002","finalidade":1}' | jq -r .id)

# 3. Cria transação DESPESA (tipo=0) com categoria RECEITA
curl -s -X POST http://localhost:5000/api/v1/transacoes \
  -H 'Content-Type: application/json' \
  -d "{
    \"descricao\": \"Conta de luz\",
    \"valor\": 150.0,
    \"tipo\": 0,
    \"categoriaId\": \"$CAT_ID\",
    \"pessoaId\": \"$PESSOA_ID\",
    \"data\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
  }" | jq .

# ESPERADO: HTTP 400 ou 422
# OBSERVADO: HTTP 500
```

## Impacto
- **Alto** — mesagem de domínio vaza em `Detailed` da resposta 500.
- Clientes da API não recebem status semântico correto.

## Correção Sugerida
Mesma do BUG-001: capturar `InvalidOperationException` no controller.

## Observação Adicional — Ordem de Atribuição
O `TransacaoService` atribui `Categoria` antes de `Pessoa` no objeto `Transacao`.
Isso significa que o BUG-002 (categoria incompatível) é lançado **antes** do BUG-001
(menor de idade) quando ambos os problemas coexistem numa mesma requisição.
Esse comportamento é coerente com a implementação, mas deve ser documentado.

## Testes que Documentam este Bug
| Camada | Arquivo | Método |
|---|---|---|
| Unitário (.NET) | `TransacaoServiceTests.cs` | `CreateAsync_CategoriaReceita_ComTipoDespesa_LancaInvalidOperationException` |
| Unitário (.NET) | `TransacaoServiceTests.cs` | `CreateAsync_CategoriaDespesa_ComTipoReceita_LancaInvalidOperationException` |
| Integração (.NET) | `TransacoesControllerTests.cs` | `PostTransacao_CategoriaReceitaComDespesa_Retorna500_BUG002` |
| E2E (Playwright) | `transacoes.spec.ts` | `BUG-002 — API retorna 500 ao usar categoria Receita em Despesa` |
