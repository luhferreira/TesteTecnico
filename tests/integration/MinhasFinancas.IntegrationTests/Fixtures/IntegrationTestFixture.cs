using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MinhasFinancas.IntegrationTests.Fixtures;

// =============================================================================
// Rotas da API (versionadas: api/v1/...)
// =============================================================================
public static class ApiRoutes
{
    public const string Pessoas          = "/api/v1/pessoas";
    public const string Categorias       = "/api/v1/categorias";
    public const string Transacoes       = "/api/v1/transacoes";
    public const string TotaisPessoas    = "/api/v1/totais/pessoas";
    public const string TotaisCategorias = "/api/v1/totais/categorias";
}

// =============================================================================
// Enums — valores inteiros conforme Domain
// =============================================================================
public enum EFinalidade { Despesa = 0, Receita = 1, Ambas = 2 }
public enum ETipo       { Despesa = 0, Receita = 1 }

// =============================================================================
// DTOs de resposta — idênticos aos da aplicação (PessoaDto, CategoriaDto, TransacaoDto)
// =============================================================================
public record PessoaResponse(Guid Id, string Nome, DateTime DataNascimento, int Idade);
public record CategoriaResponse(Guid Id, string Descricao, EFinalidade Finalidade);
public record TransacaoResponse(Guid Id, string Descricao, decimal Valor, ETipo Tipo,
    Guid CategoriaId, string CategoriaDescricao, Guid PessoaId, string PessoaNome, DateTime Data);

// =============================================================================
// DTOs de criação — idênticos aos da aplicação
// =============================================================================
public record CreatePessoaRequest(string Nome, DateTime DataNascimento);
public record UpdatePessoaRequest(string Nome, DateTime DataNascimento);
public record CreateCategoriaRequest(string Descricao, EFinalidade Finalidade);
public record CreateTransacaoRequest(
    string Descricao, decimal Valor, ETipo Tipo,
    Guid CategoriaId, Guid PessoaId, DateTime Data);

// =============================================================================
// JsonSerializerOptions — camelCase para bater com a API
// =============================================================================
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

// =============================================================================
// IntegrationTestFixture
// =============================================================================
public class IntegrationTestFixture : IAsyncLifetime
{
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
