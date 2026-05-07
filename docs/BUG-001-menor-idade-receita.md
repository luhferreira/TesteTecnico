# BUG-001 — Controller retorna 500 para Receita de menor de idade (deveria ser 400/422)

## Status
🔴 **Confirmado** — identificado na análise do código-fonte

## Descrição

A regra de negócio está corretamente implementada na entidade `Transacao.Pessoa` (setter interno):

```csharp
// MinhasFinancas.Domain/Entities/Transacao.cs
public Pessoa? Pessoa
{
    internal set
    {
        _pessoa = value;
        if (value != null)
        {
            PessoaId = value.Id;
            if (Tipo == ETipo.Receita && !value.EhMaiorDeIdade())
            {
                throw new InvalidOperationException("Menores de 18 anos não podem registrar receitas.");
            }
        }
    }
}
```

O problema é que o `TransacoesController` captura **apenas `ArgumentException`**, deixando
`InvalidOperationException` escapar para o `ExceptionMiddleware`, que retorna **500**:

```csharp
// MinhasFinancas.API/Controllers/TransacoesController.cs
catch (ArgumentException ex)
{
    return BadRequest(ex.Message);
}
// InvalidOperationException NÃO é capturada → 500
```

## Reprodução

```bash
# 1. Cria pessoa menor de idade
PESSOA_ID=$(curl -s -X POST http://localhost:5000/api/v1/pessoas \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Menor Bug","dataNascimento":"2015-01-01T00:00:00"}' | jq -r .id)

# 2. Cria categoria Receita
CAT_ID=$(curl -s -X POST http://localhost:5000/api/v1/categorias \
  -H 'Content-Type: application/json' \
  -d '{"descricao":"Salário Bug","finalidade":1}' | jq -r .id)

# 3. Tenta criar Receita para menor
curl -s -X POST http://localhost:5000/api/v1/transacoes \
  -H 'Content-Type: application/json' \
  -d "{
    \"descricao\": \"Mesada\",
    \"valor\": 100.0,
    \"tipo\": 1,
    \"categoriaId\": \"$CAT_ID\",
    \"pessoaId\": \"$PESSOA_ID\",
    \"data\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
  }" | jq .

# ESPERADO: HTTP 400 ou 422
# OBSERVADO: HTTP 500 com { "StatusCode": 500, "Message": "Ocorreu um erro interno..." }
```

## Comportamento Esperado
`HTTP 422 Unprocessable Entity` com body:
```json
{ "message": "Menores de 18 anos não podem registrar receitas." }
```

## Comportamento Observado
`HTTP 500 Internal Server Error`:
```json
{
  "StatusCode": 500,
  "Message": "Ocorreu um erro interno no servidor.",
  "Detailed": "Menores de 18 anos não podem registrar receitas."
}
```

## Impacto
- **Alto** — a mensagem de erro real chega no campo `Detailed` (exposto em produção).
- O frontend até previne o envio via UI (`isMinor && disableReceita`), mas a API não é segura.
- Qualquer client direto à API consegue criar a transação proibida se o `InvalidOperationException`
  for silenciado em alguma revisão futura do middleware.

## Correção Sugerida

```csharp
// TransacoesController.cs
catch (ArgumentException ex)
{
    return BadRequest(ex.Message);
}
catch (InvalidOperationException ex)          // ← adicionar este bloco
{
    return UnprocessableEntity(new { message = ex.Message });
}
```

## Testes que Documentam este Bug
| Camada | Arquivo | Método |
|---|---|---|
| Unitário (.NET) | `TransacaoServiceTests.cs` | `CreateAsync_MenorDeIdade_ComReceita_LancaInvalidOperationException` |
| Integração (.NET) | `TransacoesControllerTests.cs` | `PostTransacao_MenorDeIdade_ComReceita_Retorna500_BUG001` |
| E2E (Playwright) | `transacoes.spec.ts` | `BUG-001 — API retorna 500 ao criar Receita para menor` |
