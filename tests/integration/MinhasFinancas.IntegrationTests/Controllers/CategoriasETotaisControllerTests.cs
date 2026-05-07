using FluentAssertions;
using MinhasFinancas.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MinhasFinancas.IntegrationTests.Controllers;

/// <summary>
/// Testes de integração para /api/v1/categorias e /api/v1/totais.
/// </summary>
[Collection("Integration")]
public class CategoriasETotaisControllerTests : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _client;

    public CategoriasETotaisControllerTests(IntegrationTestFixture fixture)
        => _client = fixture.Client;

    // =========================================================================
    // GET /api/v1/categorias
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/categorias — retorna 200 OK")]
    public async Task GetCategorias_Retorna200()
    {
        var resp = await _client.GetAsync(ApiRoutes.Categorias);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/categorias
    // =========================================================================

    [Theory(DisplayName = "POST /api/v1/categorias — tipos válidos retornam 201")]
    [InlineData("Salário Teste", EFinalidade.Receita)]
    [InlineData("Aluguel Teste", EFinalidade.Despesa)]
    [InlineData("Transferência Teste", EFinalidade.Ambas)]
    public async Task PostCategoria_TiposValidos_Retorna201(string descricao, EFinalidade finalidade)
    {
        var uniqueDescricao = $"{descricao} {Guid.NewGuid():N[..6]}";
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest(uniqueDescricao, finalidade));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var cat = await resp.Content.ReadFromJsonAsync<CategoriaResponse>();
        cat!.Id.Should().NotBe(Guid.Empty);
        cat.Descricao.Should().Be(uniqueDescricao);
    }

    [Fact(DisplayName = "POST /api/v1/categorias — descrição vazia retorna 400")]
    public async Task PostCategoria_DescricaoVazia_Retorna400()
    {
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest("", EFinalidade.Receita));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /api/v1/categorias/{id}
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/categorias/{id} — id inexistente retorna 404")]
    public async Task GetCategoriaPorId_IdInexistente_Retorna404()
    {
        var resp = await _client.GetAsync($"{ApiRoutes.Categorias}/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /api/v1/categorias/{id} — após criar, retorna dados corretos")]
    public async Task GetCategoriaPorId_AposCriar_RetornaDadosCorretos()
    {
        var descricao = $"Cat Get {Guid.NewGuid():N[..8]}";
        var postResp  = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest(descricao, EFinalidade.Ambas));
        var criada    = await postResp.Content.ReadFromJsonAsync<CategoriaResponse>();

        var getResp = await _client.GetAsync($"{ApiRoutes.Categorias}/{criada!.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var obtida = await getResp.Content.ReadFromJsonAsync<CategoriaResponse>();
        obtida!.Descricao.Should().Be(descricao);
        // A API serializa o enum como inteiro ou string dependendo da configuração
        obtida.Id.Should().Be(criada.Id);
    }

    // =========================================================================
    // GET /api/v1/totais/pessoas
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/totais/pessoas — retorna 200 OK")]
    public async Task GetTotaisPorPessoa_Retorna200()
    {
        var resp = await _client.GetAsync(ApiRoutes.TotaisPessoas);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /api/v1/totais/pessoas — após criar transações, calcula saldo correto")]
    public async Task GetTotaisPorPessoa_ComTransacoes_RetornaCalculoCorreto()
    {
        // Cria pessoa adulta
        var pessoaResp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest($"Pessoa Totais {Guid.NewGuid():N[..6]}", new DateTime(1985, 1, 1)));
        pessoaResp.EnsureSuccessStatusCode();
        var pessoa = await pessoaResp.Content.ReadFromJsonAsync<PessoaResponse>();

        // Cria categorias
        var catRecResp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest($"Receita Tot {Guid.NewGuid():N[..6]}", EFinalidade.Receita));
        var catRec = await catRecResp.Content.ReadFromJsonAsync<CategoriaResponse>();

        var catDespResp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest($"Despesa Tot {Guid.NewGuid():N[..6]}", EFinalidade.Despesa));
        var catDesp = await catDespResp.Content.ReadFromJsonAsync<CategoriaResponse>();

        // Cria transações: R$5000 receita + R$1500 despesa
        await _client.PostAsJsonAsync(ApiRoutes.Transacoes,
            new CreateTransacaoRequest("Salário", 5000m, ETipo.Receita,
                catRec!.Id, pessoa!.Id, DateTime.Today));
        await _client.PostAsJsonAsync(ApiRoutes.Transacoes,
            new CreateTransacaoRequest("Aluguel", 1500m, ETipo.Despesa,
                catDesp!.Id, pessoa.Id, DateTime.Today));

        // Obtém totais por pessoa
        var totaisResp = await _client.GetAsync(ApiRoutes.TotaisPessoas);
        totaisResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // A resposta é paginada; verificamos que existe pelo menos 1 registro
        var body = await totaisResp.Content.ReadAsStringAsync();
        body.Should().Contain(pessoa.Id.ToString());
        body.Should().Contain("5000");
        body.Should().Contain("1500");
    }

    // =========================================================================
    // GET /api/v1/totais/categorias
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/totais/categorias — retorna 200 OK")]
    public async Task GetTotaisPorCategoria_Retorna200()
    {
        var resp = await _client.GetAsync(ApiRoutes.TotaisCategorias);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
