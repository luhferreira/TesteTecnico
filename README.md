# Minhas Finanças — Estratégia de Testes Automatizados

Suite completa de testes automatizados para o sistema **Minhas Finanças**, cobrindo regras de negócio, contratos HTTP, integração com banco e fluxos reais de usuário no browser.

O código-fonte da aplicação reside em repositório separado e **não está incluído neste projeto**.

## Resultados da execução

✅ **47 testes unitários (.NET)**  
✅ **81 testes de integração (API)**  
✅ **18 testes unitários frontend (Vitest)**  
✅ **Fluxos E2E automatizados com Playwright**  
✅ **3 bugs reais identificados e documentados**

---

# Objetivo

Validar o sistema em diferentes níveis da pirâmide de testes, garantindo:

- integridade das regras de negócio
- consistência dos contratos HTTP
- isolamento entre cenários
- prevenção de regressões
- cobertura de fluxos críticos do usuário

---

# Estrutura do repositório

```text
minhas-financas-tests/
├── .github/workflows/ci.yml
├── tests/
│   ├── unit/
│   │   └── MinhasFinancas.UnitTests/
│   │       ├── Domain/
│   │       └── Application/
│   │
│   ├── integration/
│   │   └── MinhasFinancas.IntegrationTests/
│   │       ├── Controllers/
│   │       └── Fixtures/
│   │
│   └── e2e/
│       ├── vitest/
│       └── playwright/
│
└── docs/
```

---

# Pirâmide de testes

```text
         ▲
        /E2E\
       /─────\
      /       \
     / Integr. \
    /───────────\
   /             \
  /   Unitários   \
 /─────────────────\
```

## Unit Tests

Cobertura de regras de negócio em isolamento.

### Backend (.NET + xUnit)

Cobertura de:

- cálculo de idade
- validação de maioridade
- compatibilidade categoria × tipo
- setters com regras internas da entidade
- services com mocks de repositório
- cenários de erro e cascata

Arquivos principais:

- `PessoaTests.cs`
- `CategoriaTests.cs`
- `TransacaoTests.cs`
- `PessoaServiceTests.cs`
- `TransacaoServiceTests.cs`

---

### Frontend (Vitest)

Cobertura de validações reutilizadas pelo frontend:

- `calcularIdade`
- `categoriaPermiteTipo`
- `validarTransacao`

Arquivo principal:

- `validations.test.ts`

Total:

**18 cenários automatizados**

---

# Integration Tests

Testes executados contra endpoints HTTP reais.

Cobertura de:

- CRUD completo
- validação de DTOs
- entidades inexistentes
- paginação
- cálculo de totais
- exclusão em cascata
- regras de negócio

Tecnologias:

- xUnit
- HttpClient
- FluentAssertions
- SQLite via Docker

Arquivos principais:

- `PessoasControllerTests.cs`
- `TransacoesControllerTests.cs`
- `CategoriasETotaisControllerTests.cs`

Total:

**81 testes de integração**

---

# E2E Tests

Automação de fluxos reais do usuário em navegador.

Cobertura de:

- cadastro de pessoas
- cadastro de transações
- validações visuais
- persistência de dados
- tratamento de erros

Tecnologias:

- Playwright
- Bun
- Page Object Model

Arquivos principais:

- `pessoas.spec.ts`
- `transacoes.spec.ts`

---

# Bugs encontrados durante a validação

Durante a execução da suite, foram identificados bugs reais na aplicação.

## BUG-001 — Regra de menor de idade retorna HTTP 500

### Cenário

Ao cadastrar uma receita para uma pessoa menor de idade:

A regra de negócio é aplicada corretamente, porém a exceção não é tratada pelo controller.

### Resultado atual

```http
500 Internal Server Error
```

### Resultado esperado

```http
422 Unprocessable Entity
```

### Impacto

O usuário recebe erro interno do sistema ao invés de feedback de validação.

Documentação:

`docs/BUG-001-menor-idade-receita.md`

---

## BUG-002 — Categoria incompatível retorna HTTP 500

### Cenário

Ao cadastrar transação com categoria incompatível.

### Resultado atual

```http
500 Internal Server Error
```

### Resultado esperado

```http
422 Unprocessable Entity
```

### Impacto

Falha de comunicação da regra de negócio para o consumidor da API.

Documentação:

`docs/BUG-002-categoria-incompativel.md`

---

## BUG-003 — Exclusão em cascata depende do EF Core

### Cenário

`PessoaService.DeleteAsync()` não remove transações explicitamente.

### Resultado atual

Funciona apenas porque o EF Core executa cascade delete.

### Risco

Mudança futura de provider ou configuração pode quebrar comportamento.

Documentação:

`docs/BUG-003-exclusao-cascata.md`

---

# Causa raiz dos bugs 001 e 002

As regras estão implementadas corretamente na entidade `Transacao`.

O problema está no tratamento das exceções no controller:

```csharp
catch (ArgumentException ex)
{
    return BadRequest(ex.Message);
}
```

`InvalidOperationException` não é tratada e acaba sendo convertida em HTTP 500.

## Correção sugerida

```csharp
catch (InvalidOperationException ex)
{
    return UnprocessableEntity(new
    {
        message = ex.Message
    });
}
```

---

# Pré-requisitos

| Ferramenta | Versão | Uso |
|------------|--------|-----|
| .NET SDK | 9.0+ | Unit + Integration |
| Bun | 1.1+ | Vitest + Playwright |
| Docker | 24+ | API + frontend |

---

# Como executar

## 1. Testes unitários (.NET)

```bash
cd tests/unit
dotnet restore
dotnet test
```

Cobertura:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 2. Testes frontend (Vitest)

```bash
cd tests/e2e
bun install
bun run test:unit
```

---

## 3. Testes de integração

Subir API:

```bash
docker-compose up -d api
```

Executar:

```bash
cd tests/integration
dotnet test
```

---

## 4. Testes E2E

Subir stack completa:

```bash
docker-compose up -d
```

Executar:

```bash
cd tests/e2e
bun run test:e2e
```

---

# Pipeline CI/CD

O workflow automatizado executa:

```text
unit-tests
   ↓
integration-tests
   ↓
e2e-tests
```

Garantindo validação progressiva em todos os níveis.

---

# Decisões técnicas

| Escolha | Motivação |
|---------|------------|
| xUnit | padrão do ecossistema .NET |
| FluentAssertions | mensagens de falha mais legíveis |
| NSubstitute | mocks type-safe |
| Vitest | integração nativa com Vite |
| Playwright | automação robusta + API testing |
| Page Object Model | manutenção simplificada |
| Docker | ambiente reproduzível |

---

# Principais competências demonstradas

- Estratégia de testes
- Testes de API
- Automação de interface
- Testes de contrato
- Investigação e documentação de bugs
- Análise de causa raiz
- Integração e entrega contínua (CI/CD)
- Arquitetura de testes
- Testes defensivos
- Prevenção de regressões
