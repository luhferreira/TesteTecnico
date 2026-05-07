# Minhas Finanças — Repositório de Testes

Repositório contendo **exclusivamente os testes automatizados** do sistema Minhas Finanças.
O código-fonte da aplicação reside em repositório separado e **não está incluído aqui**.

---

## Estrutura do Repositório

```
minhas-financas-tests/
├── .github/workflows/ci.yml
├── tests/
│   ├── unit/MinhasFinancas.UnitTests/
│   │   ├── Domain/
│   │   │   ├── PessoaTests.cs           # Idade, EhMaiorDeIdade
│   │   │   ├── CategoriaTests.cs        # PermiteTipo
│   │   │   └── TransacaoTests.cs        # Setters com regras de negócio
│   │   └── Application/
│   │       ├── TransacaoServiceTests.cs # Regras via service + mocks
│   │       └── PessoaServiceTests.cs    # CRUD + comportamento de cascade
│   ├── integration/MinhasFinancas.IntegrationTests/
│   │   ├── Controllers/
│   │   │   ├── PessoasControllerTests.cs
│   │   │   ├── TransacoesControllerTests.cs     # Documenta BUG-001 e BUG-002
│   │   │   └── CategoriasETotaisControllerTests.cs
│   │   └── Fixtures/IntegrationTestFixture.cs   # Factory + DTOs + rotas
│   └── e2e/
│       ├── vitest/src/validations.ts             # Lógica espelhada do frontend
│       ├── vitest/__tests__/validations.test.ts  # 18 casos unitários
│       └── playwright/
│           ├── fixtures/index.ts                 # Setup/teardown via API
│           ├── page-objects/index.ts             # POM baseado nos componentes reais
│           └── tests/
│               ├── pessoas.spec.ts
│               └── transacoes.spec.ts            # Documenta BUG-001, BUG-002, BUG-003
└── docs/
    ├── BUG-001-menor-idade-receita.md
    ├── BUG-002-categoria-incompativel.md
    └── BUG-003-exclusao-cascata.md
```

---

## Pré-requisitos

| Ferramenta | Versão | Uso |
|---|---|---|
| .NET SDK | 9.0+ | Testes unitários e de integração |
| Bun | 1.1+ | Vitest + Playwright |
| Docker | 24+ | API + frontend para integração e E2E |

---

## Configuração inicial — ProjectReferences

Ajuste os caminhos nos `.csproj` conforme a localização relativa dos repositórios:

```
projetos/
├── minhas-financas/        ← repositório da aplicação
│   └── api/
│       ├── MinhasFinancas.API/
│       ├── MinhasFinancas.Application/
│       ├── MinhasFinancas.Domain/
│       └── MinhasFinancas.Infrastructure/
└── minhas-financas-tests/  ← este repositório
```

---

## Como Executar os Testes

### 1. Testes Unitários — .NET (sem dependências externas)

```bash
cd tests/unit
dotnet restore
dotnet test --verbosity normal

# Com cobertura
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### 2. Testes Unitários — Frontend / Vitest (sem dependências externas)

```bash
cd tests/e2e
bun install
bun run test:unit           # execução única
bun run test:unit:watch     # modo watch
bun run test:unit:coverage  # com cobertura em ./coverage
```

### 3. Testes de Integração (requer API via Docker)

```bash
# No repositório da aplicação:
docker-compose up -d api

# Neste repositório:
cd tests/integration
dotnet restore
dotnet test --verbosity normal
```

> **Alternativa sem Docker:** Descomente `CustomWebApplicationFactory` em
> `Fixtures/IntegrationTestFixture.cs` e use `Factory.CreateClient()` para banco InMemory.

### 4. Testes E2E — Playwright (requer stack completa)

```bash
# No repositório da aplicação:
docker-compose up -d

# Neste repositório:
cd tests/e2e
bun install
bun run playwright:install   # primeira vez
bun run test:e2e             # headless
bun run test:e2e:headed      # com browser visível
bun run test:e2e:ui          # UI interativa
```

### 5. Todos os testes

```bash
cd tests/e2e
bun run test:all
```

---

## Pirâmide de Testes

```
         ▲
        /E2E\         Playwright — fluxos reais no browser
       /─────\        pessoas.spec + transacoes.spec
      /       \
     / Integr. \      xUnit + HttpClient → API via Docker
    /───────────\     CRUD, regras de negócio, status HTTP, cascata
   /             \
  /   Unitários   \   xUnit + NSubstitute + Vitest
 /─────────────────\  Entidades, Services (mocks), validações frontend
```

### Unitária
- `PessoaTests` — `Idade` e `EhMaiorDeIdade` com boundary de 18 anos
- `CategoriaTests` — `PermiteTipo` para todas as combinações `EFinalidade × ETipo`
- `TransacaoTests` — setters `Pessoa` e `Categoria` que contêm as regras de negócio reais
- `TransacaoServiceTests` — service completo com repositórios mockados
- `PessoaServiceTests` — CRUD, cascata delegada ao EF, casos de erro
- `validations.test.ts` (Vitest) — `calcularIdade`, `categoriaPermiteTipo`, `validarTransacao`

### Integração
- Testa endpoints HTTP reais contra SQLite via Docker (ou InMemory via Factory)
- Documenta BUGs com `StatusCode.Should().Be(500)` nos testes de negócio
- Cobre criação válida, validação de DTO (400), entidades inexistentes (404), cascata

### E2E
- Page Object Model baseado nos componentes reais (`TransacaoForm.tsx`, `PessoaForm.tsx`, `routes.ts`)
- Fixtures com setup/teardown via API — dados limpos entre testes
- Testes de API direta para documentar status HTTP dos BUGs

---

## Bugs Encontrados

| # | Severidade | Descrição | Arquivo |
|---|---|---|---|
| BUG-001 | 🔴 Alto | `InvalidOperationException` de menor de idade não é capturada no controller → HTTP 500 em vez de 422 | [docs/BUG-001](docs/BUG-001-menor-idade-receita.md) |
| BUG-002 | 🔴 Alto | `InvalidOperationException` de categoria incompatível não é capturada → HTTP 500 em vez de 422 | [docs/BUG-002](docs/BUG-002-categoria-incompativel.md) |
| BUG-003 | 🟡 Médio | `PessoaService.DeleteAsync` não exclui transações explicitamente — cascata depende do EF Core | [docs/BUG-003](docs/BUG-003-exclusao-cascata.md) |

### Causa raiz dos BUG-001 e BUG-002

As regras estão corretamente implementadas nos **setters internos da entidade `Transacao`**
(que lançam `InvalidOperationException`). O problema está no controller:

```csharp
// TransacoesController.cs — só captura ArgumentException
catch (ArgumentException ex)
{
    return BadRequest(ex.Message);  // 400
}
// InvalidOperationException não é capturada → ExceptionMiddleware → 500
```

**Correção sugerida:**
```csharp
catch (InvalidOperationException ex)
{
    return UnprocessableEntity(new { message = ex.Message });  // 422
}
```

---

## Justificativa das Escolhas

| Escolha | Motivo |
|---|---|
| **xUnit** | Framework padrão do ecossistema .NET 9; melhor suporte a `IClassFixture` e paralelismo |
| **NSubstitute** | API type-safe para mocks; `Received.InOrder` verifica sequência de chamadas |
| **FluentAssertions** | Mensagens de falha descritivas; `.BeOneOf()` para múltiplos status HTTP válidos |
| **Bogus** | Dados realistas com seed reproduzível — sem fixtures manuais frágeis |
| **Vitest** | Toolchain nativo do projeto (Vite); mais rápido que Jest em TypeScript |
| **Playwright** | `APIRequestContext` permite setup/teardown via HTTP na mesma ferramenta; POM nativo |
| **Page Object Model** | Seletores centralizados nos componentes reais — mudança de UI = 1 arquivo a alterar |
| **WebApplicationFactory + InMemory** | Banco isolado por `Guid.NewGuid()` — testes paralelos sem conflito |

---

## CI/CD

O workflow `.github/workflows/ci.yml` executa 3 estágios sequenciais:

```
unit-tests + vitest  →  integration-tests  →  e2e-tests
(sem dependências)       (Docker API)          (stack completa)
```

**Antes de ativar o CI:**
1. Substitua `your-org/minhas-financas` pelo repositório real da aplicação
2. Configure o secret `APP_REPO_TOKEN` com acesso de leitura ao repositório da aplicação
