using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhasFinancas.Domain.Entities;
using MinhasFinancas.Infrastructure.Data;

namespace MinhasFinancas.IntegrationTests.Fixtures;

// =============================================================================
// Rotas da API (versionadas: api/v1/...)
// =============================================================================
public static class ApiRoutes
{
    public const string Pessoas    = "/api/v1/pessoas";
    public const string Categorias = "/api/v1/categorias";
    public const string Transacoes = "/api/v1/transacoes";
    public const string TotaisPessoas    = "/api/v1/totais/pessoas";
    public const string TotaisCategorias = "/api/v1/totais/categorias";
}

// =============================================================================
// DTOs espelho (idênticos aos da aplicação)
// Use ProjectReference para referenciar os tipos reais em vez destes.
// =============================================================================

public record PessoaResponse(Guid Id, string Nome, DateTime DataNascimento, int Idade);
public record CategoriaResponse(Guid Id, string Descricao, string Finalidade);
public record TransacaoResponse(Guid Id, string Descricao, decimal Valor, string Tipo,
    Guid CategoriaId, string CategoriaDescricao, Guid PessoaId, string PessoaNome, DateTime Data);

public record CreatePessoaRequest(string Nome, DateTime DataNascimento);
public record UpdatePessoaRequest(string Nome, DateTime DataNascimento);

/// <summary>
/// Finalidade (espelho de Categoria.EFinalidade).
/// Valores: 0=Despesa, 1=Receita, 2=Ambas
/// </summary>
public enum EFinalidade { Despesa = 0, Receita = 1, Ambas = 2 }

/// <summary>
/// Tipo de transação (espelho de Transacao.ETipo).
/// Valores: 0=Despesa, 1=Receita
/// </summary>
public enum ETipo { Despesa = 0, Receita = 1 }

public record CreateCategoriaRequest(string Descricao, EFinalidade Finalidade);
public record CreateTransacaoRequest(
    string Descricao, decimal Valor, ETipo Tipo,
    Guid CategoriaId, Guid PessoaId, DateTime Data);

public record ErrorResponse(int StatusCode, string Message, string Detailed);

// =============================================================================
// CustomWebApplicationFactory
// Substitui SQLite por banco InMemory para isolamento total dos testes.
// =============================================================================

/// <summary>
/// Factory que inicializa a API com banco InMemory.
/// Requer ProjectReference para MinhasFinancas.API.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContext SQLite real
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MinhasFinancasDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Usa banco InMemory com nome único por instância
            services.AddDbContext<MinhasFinancasDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            // Garante criação do banco
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MinhasFinancasDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}

// =============================================================================
// IntegrationTestFixture
// =============================================================================

/// <summary>
/// Fixture compartilhada pelos testes de integração.
/// Expõe HttpClient configurado via WebApplicationFactory (InMemory)
/// OU apontando para a API real via Docker (localhost:5000).
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    // Descomente a linha abaixo quando o ProjectReference estiver configurado:
    // public CustomWebApplicationFactory Factory { get; } = new();
    // public HttpClient Client => Factory.CreateClient();

    /// <summary>
    /// HttpClient apontando para a API em execução via Docker.
    /// Altere para usar Factory.CreateClient() com banco InMemory quando possível.
    /// </summary>
    public HttpClient Client { get; private set; } = default!;

    public Task InitializeAsync()
    {
        Client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
