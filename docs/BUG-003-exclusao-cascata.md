# BUG-003 — Exclusão em cascata de Transações ao excluir Pessoa (a verificar)

## Status
🟡 **A Confirmar** — depende da configuração do EF Core no DbContext

## Descrição

O `PessoaService.DeleteAsync` chama apenas:

```csharp
// MinhasFinancas.Application/Services/PessoaService.cs
public async Task DeleteAsync(Guid id)
{
    await _unitOfWork.Pessoas.DeleteAsync(id);
    await _unitOfWork.SaveChangesAsync();
}
```

Não há nenhuma chamada explícita a `_unitOfWork.Transacoes.DeleteAsync(...)`.
A exclusão em cascata depende **inteiramente** de `DeleteBehavior.Cascade`
configurado no `MinhasFinancasDbContext`.

Se o `DbContext` não tiver o comportamento de cascata configurado para a relação
`Pessoa → Transacoes`, dois cenários de falha podem ocorrer:

| Cenário | Resultado |
|---|---|
| FK restritiva (padrão SQLite sem cascade) | `DELETE /api/v1/pessoas/{id}` retorna **500** (erro de FK) |
| FK nullable sem cascade | `DELETE` retorna **204**, mas transações ficam **órfãs** no banco |

## Onde Verificar no Código

```csharp
// MinhasFinancas.Infrastructure/Data/MinhasFinancasDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Verificar se existe:
    modelBuilder.Entity<Transacao>()
        .HasOne(t => t.Pessoa)
        .WithMany(p => p.Transacoes)
        .HasForeignKey(t => t.PessoaId)
        .OnDelete(DeleteBehavior.Cascade);  // ← esta linha é necessária
}
```

## Reprodução

```bash
# 1. Cria adulto
PESSOA_ID=$(curl -s -X POST http://localhost:5000/api/v1/pessoas \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Pessoa Cascata","dataNascimento":"1990-01-01T00:00:00"}' | jq -r .id)

# 2. Cria categoria e transação vinculadas
CAT_ID=$(curl -s -X POST http://localhost:5000/api/v1/categorias \
  -H 'Content-Type: application/json' \
  -d '{"descricao":"Despesa Cascata","finalidade":0}' | jq -r .id)

TX_ID=$(curl -s -X POST http://localhost:5000/api/v1/transacoes \
  -H 'Content-Type: application/json' \
  -d "{\"descricao\":\"Gasto\",\"valor\":100,\"tipo\":0,
       \"categoriaId\":\"$CAT_ID\",\"pessoaId\":\"$PESSOA_ID\",
       \"data\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}" | jq -r .id)

# 3. Deleta a pessoa
STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X DELETE http://localhost:5000/api/v1/pessoas/$PESSOA_ID)
echo "DELETE pessoa status: $STATUS"
# ESPERADO: 204
# SE 500: cascade não configurado, FK rejeitou

# 4. Verifica que a transação foi excluída junto
STATUS_TX=$(curl -s -o /dev/null -w "%{http_code}" \
  http://localhost:5000/api/v1/transacoes/$TX_ID)
echo "GET transação status: $STATUS_TX"
# ESPERADO: 404 (excluída em cascata)
# SE 200: transação órfã — cascade não configurado
```

## Impacto
- **Alto** (se 500): exclusão de pessoas com histórico financeiro falha completamente.
- **Alto** (se órfão): dados inconsistentes — transações sem dono distorcem totais e relatórios.

## Correção Sugerida
Garantir no `DbContext`:
```csharp
.OnDelete(DeleteBehavior.Cascade)
```
para a relação `Pessoa → Transacoes` **e** para `Categoria → Transacoes`.

## Testes que Documentam este Comportamento
| Camada | Arquivo | Método |
|---|---|---|
| Unitário (.NET) | `PessoaServiceTests.cs` | `DeleteAsync_NaoChamaTransacaoRepositorioExplicitamente` |
| Integração (.NET) | `PessoasControllerTests.cs` | `DeletePessoa_TransacoesVinculadas_SaoRemovidasEmCascata` |
| E2E (Playwright) | `pessoas.spec.ts` | `BUG-003 — DELETE pessoa com transações: verifica cascata` |
