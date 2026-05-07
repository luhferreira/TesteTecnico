using FluentAssertions;
using MinhasFinancas.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MinhasFinancas.IntegrationTests.Controllers;

/// <summary>
/// Testes de integração para POST/GET/PUT/DELETE /api/v1/pessoas.
/// Pré-requisito: docker-compose up -d api (ou WebApplicationFactory com InMemory).
/// </summary>
[Collection("Integration")]
public class PessoasControllerTests : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _client;

    public PessoasControllerTests(IntegrationTestFixture fixture)
        => _client = fixture.Client;

    // =========================================================================
    // GET /api/v1/pessoas
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/pessoas — retorna 200 OK")]
    public async Task GetPessoas_Retorna200()
    {
        var response = await _client.GetAsync(ApiRoutes.Pessoas);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/pessoas
    // =========================================================================

    [Fact(DisplayName = "POST /api/v1/pessoas — dados válidos retornam 201 com Guid")]
    public async Task PostPessoa_DadosValidos_Retorna201ComGuid()
    {
        var payload  = new CreatePessoaRequest("Integração Adulto", new DateTime(1990, 5, 10));
        var response = await _client.PostAsJsonAsync(ApiRoutes.Pessoas, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await response.Content.ReadFromJsonAsync<PessoaResponse>();
        criada!.Id.Should().NotBe(Guid.Empty);
        criada.Nome.Should().Be("Integração Adulto");
    }

    [Fact(DisplayName = "POST /api/v1/pessoas — nome vazio retorna 400")]
    public async Task PostPessoa_NomeVazio_Retorna400()
    {
        var payload  = new CreatePessoaRequest("", new DateTime(1990, 1, 1));
        var response = await _client.PostAsJsonAsync(ApiRoutes.Pessoas, payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/v1/pessoas — data de nascimento futura retorna 400")]
    public async Task PostPessoa_DataNascimentoFutura_Retorna400()
    {
        var payload  = new CreatePessoaRequest("Pessoa Futura", DateTime.Today.AddDays(1));
        var response = await _client.PostAsJsonAsync(ApiRoutes.Pessoas, payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /api/v1/pessoas/{id}
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/pessoas/{id} — id inexistente retorna 404")]
    public async Task GetPessoaPorId_IdInexistente_Retorna404()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Pessoas}/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /api/v1/pessoas/{id} — após criar, retorna dados corretos")]
    public async Task GetPessoaPorId_AposCriar_RetornaDadosCorretos()
    {
        var payload = new CreatePessoaRequest($"Pessoa Get {Guid.NewGuid():N[..6]}", new DateTime(1985, 3, 22));
        var postResp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas, payload);
        var criada   = await postResp.Content.ReadFromJsonAsync<PessoaResponse>();

        var getResp = await _client.GetAsync($"{ApiRoutes.Pessoas}/{criada!.Id}");

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var pessoa = await getResp.Content.ReadFromJsonAsync<PessoaResponse>();
        pessoa!.Nome.Should().Be(payload.Nome);
        pessoa.Idade.Should().BeGreaterThan(0);
    }

    // =========================================================================
    // PUT /api/v1/pessoas/{id}
    // =========================================================================

    [Fact(DisplayName = "PUT /api/v1/pessoas/{id} — atualiza nome e retorna 204")]
    public async Task PutPessoa_DadosValidos_Retorna204()
    {
        // Cria pessoa
        var postResp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest("Original", new DateTime(1990, 1, 1)));
        var criada = await postResp.Content.ReadFromJsonAsync<PessoaResponse>();

        // Atualiza
        var dto      = new UpdatePessoaRequest("Atualizado", new DateTime(1990, 1, 1));
        var putResp  = await _client.PutAsJsonAsync($"{ApiRoutes.Pessoas}/{criada!.Id}", dto);
        putResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verifica
        var getResp  = await _client.GetAsync($"{ApiRoutes.Pessoas}/{criada.Id}");
        var atualizado = await getResp.Content.ReadFromJsonAsync<PessoaResponse>();
        atualizado!.Nome.Should().Be("Atualizado");
    }

    [Fact(DisplayName = "PUT /api/v1/pessoas/{id} — id inexistente retorna 404")]
    public async Task PutPessoa_IdInexistente_Retorna404()
    {
        var dto     = new UpdatePessoaRequest("X", DateTime.Today.AddYears(-20));
        var putResp = await _client.PutAsJsonAsync($"{ApiRoutes.Pessoas}/{Guid.NewGuid()}", dto);
        putResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // DELETE /api/v1/pessoas/{id}
    // =========================================================================

    [Fact(DisplayName = "DELETE /api/v1/pessoas/{id} — exclui pessoa e retorna 204")]
    public async Task DeletePessoa_Retorna204()
    {
        var postResp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest("Para Deletar", new DateTime(1990, 1, 1)));
        var criada = await postResp.Content.ReadFromJsonAsync<PessoaResponse>();

        var delResp = await _client.DeleteAsync($"{ApiRoutes.Pessoas}/{criada!.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"{ApiRoutes.Pessoas}/{criada.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifica exclusão em cascata: transações vinculadas devem ser removidas
    /// quando a pessoa é excluída. Depende de DeleteBehavior.Cascade no EF.
    /// Ver BUG-003.
    /// </summary>
    [Fact(DisplayName = "DELETE /api/v1/pessoas/{id} — transações vinculadas são removidas em cascata")]
    public async Task DeletePessoa_TransacoesVinculadas_SaoRemovidasEmCascata()
    {
        // Cria pessoa adulta
        var pessoa = await CriarPessoaAdultaAsync("Cascata Delete");

        // Cria categoria Despesa
        var catResp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest("Despesa Cascata", EFinalidade.Despesa));
        var cat = await catResp.Content.ReadFromJsonAsync<CategoriaResponse>();

        // Cria transação vinculada à pessoa
        var txResp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes,
            new CreateTransacaoRequest("Gasto cascata", 50m, ETipo.Despesa,
                cat!.Id, pessoa.Id, DateTime.Today));

        // BUG-003: se não houver cascade no EF, este Post pode retornar 500
        // ao tentar deletar a pessoa com transações vinculadas.
        txResp.IsSuccessStatusCode.Should().BeTrue("a transação deve ser criada com sucesso");
        var tx = await txResp.Content.ReadFromJsonAsync<TransacaoResponse>();

        // Deleta a pessoa
        var delResp = await _client.DeleteAsync($"{ApiRoutes.Pessoas}/{pessoa.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "a pessoa deve ser excluída sem erro de FK");

        // Transação deve ter sido excluída junto
        var txCheck = await _client.GetAsync($"{ApiRoutes.Transacoes}/{tx!.Id}");
        txCheck.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a transação deve ter sido excluída em cascata");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<PessoaResponse> CriarPessoaAdultaAsync(string nome)
    {
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest(nome, new DateTime(1985, 6, 15)));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PessoaResponse>())!;
    }
}
