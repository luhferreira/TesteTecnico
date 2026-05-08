using FluentAssertions;
using MinhasFinancas.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MinhasFinancas.IntegrationTests.Controllers;

/// <summary>
/// Testes de integração para POST/GET /api/v1/transacoes.
///
/// Documenta o comportamento HTTP real das regras de negócio:
///
/// BUG-001: Menor de idade com Receita — InvalidOperationException não é tratada
///          pelo controller (só captura ArgumentException), portanto retorna 500.
///          ESPERADO: 400 ou 422. OBSERVADO: 500.
///
/// BUG-002: Categoria incompatível — mesma situação, retorna 500 em vez de 400/422.
/// </summary>
[Collection("Integration")]
public class TransacoesControllerTests : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _client;

    public TransacoesControllerTests(IntegrationTestFixture fixture)
        => _client = fixture.Client;

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<PessoaResponse> CriarAdultoAsync(string nome = "Adulto Tx")
    {
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest(nome, new DateTime(1985, 6, 15)));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PessoaResponse>(JsonOptions.Default))!;
    }

    private async Task<PessoaResponse> CriarMenorAsync(string nome = "Menor Tx")
    {
        var nascimento = DateTime.Today.AddYears(-10);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Pessoas,
            new CreatePessoaRequest(nome, nascimento));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PessoaResponse>(JsonOptions.Default))!;
    }

    private async Task<CategoriaResponse> CriarCatAsync(string descricao, EFinalidade finalidade)
    {
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Categorias,
            new CreateCategoriaRequest(descricao, finalidade));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoriaResponse>(JsonOptions.Default))!;
    }

    // =========================================================================
    // GET /api/v1/transacoes
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/transacoes — retorna 200 OK")]
    public async Task GetTransacoes_Retorna200()
    {
        var response = await _client.GetAsync(ApiRoutes.Transacoes);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST — Happy path
    // =========================================================================

    [Fact(DisplayName = "POST /api/v1/transacoes — adulto + categoria Receita + tipo Receita retorna 201")]
    public async Task PostTransacao_AdultoReceitaValida_Retorna201()
    {
        var adulto = await CriarAdultoAsync($"Adulto {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Salário {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Receita);

        var dto    = new CreateTransacaoRequest("Salário mensal", 5000m, ETipo.Receita,
            cat.Id, adulto.Id, DateTime.Today);
        var resp   = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var tx = await resp.Content.ReadFromJsonAsync<TransacaoResponse>(JsonOptions.Default);
        tx!.Id.Should().NotBe(Guid.Empty);
        tx.Valor.Should().Be(5000m);
    }

    [Fact(DisplayName = "POST /api/v1/transacoes — menor de idade + Despesa retorna 201")]
    public async Task PostTransacao_MenorDeIdade_ComDespesa_Retorna201()
    {
        var menor = await CriarMenorAsync($"Menor {Guid.NewGuid().ToString("N")[..6]}");
        var cat   = await CriarCatAsync($"Lanche {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Despesa);

        var dto   = new CreateTransacaoRequest("Lanche", 15m, ETipo.Despesa,
            cat.Id, menor.Id, DateTime.Today);
        var resp  = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            "menores de idade podem ter despesas");
    }

    [Theory(DisplayName = "POST /api/v1/transacoes — categoria Ambas aceita Receita e Despesa")]
    [InlineData(ETipo.Receita)]
    [InlineData(ETipo.Despesa)]
    public async Task PostTransacao_CategoriaAmbas_QualquerTipo_Retorna201(ETipo tipo)
    {
        var adulto = await CriarAdultoAsync($"Adulto Ambas {tipo} {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Transf {tipo} {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Ambas);

        var dto  = new CreateTransacaoRequest($"Transferência {tipo}", 200m, tipo,
            cat.Id, adulto.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // =========================================================================
    // POST — BUG-001: Menor de idade com Receita
    // Documenta que o controller retorna 500 em vez de 400/422
    // porque captura apenas ArgumentException, não InvalidOperationException.
    // =========================================================================

    [Fact(DisplayName = "BUG-001 — POST Receita para menor retorna 500 (deveria ser 400/422)")]
    public async Task PostTransacao_MenorDeIdade_ComReceita_Retorna500_BUG001()
    {
        var menor = await CriarMenorAsync($"Menor Receita {Guid.NewGuid().ToString("N")[..6]}");
        var cat   = await CriarCatAsync($"Salário Bug {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Receita);

        var dto  = new CreateTransacaoRequest("Mesada indevida", 100m, ETipo.Receita,
            cat.Id, menor.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        // BUG: controller só captura ArgumentException. InvalidOperationException
        // lançada pela entidade Transacao.Pessoa escala para o ExceptionMiddleware → 500.
        // O comportamento CORRETO seria 400 ou 422.
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            "BUG-001: controller não trata InvalidOperationException, retorna 500");

        // Para fixar: capturar InvalidOperationException no controller e retornar 422.
    }

    // =========================================================================
    // POST — BUG-002: Categoria incompatível
    // =========================================================================

    [Fact(DisplayName = "BUG-002 — POST Despesa com categoria Receita retorna 500 (deveria ser 400/422)")]
    public async Task PostTransacao_CategoriaReceitaComDespesa_Retorna500_BUG002()
    {
        var adulto = await CriarAdultoAsync($"Adulto Compat {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Salário Compat {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Receita);

        var dto  = new CreateTransacaoRequest("Conta de luz", 150m, ETipo.Despesa,
            cat.Id, adulto.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        // BUG: InvalidOperationException do setter Transacao.Categoria → 500
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            "BUG-002: controller não trata InvalidOperationException de categoria incompatível");
    }

    [Fact(DisplayName = "BUG-002 — POST Receita com categoria Despesa retorna 500 (deveria ser 400/422)")]
    public async Task PostTransacao_CategoriaDespesaComReceita_Retorna500_BUG002()
    {
        var adulto = await CriarAdultoAsync($"Adulto Desp Rec {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Mercado Rec {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Despesa);

        var dto  = new CreateTransacaoRequest("Freelance", 800m, ETipo.Receita,
            cat.Id, adulto.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            "BUG-002: controller não trata InvalidOperationException de categoria incompatível");
    }

    // =========================================================================
    // POST — Validações de DTO (ModelState)
    // =========================================================================

    [Fact(DisplayName = "POST /api/v1/transacoes — valor zero retorna 400 (ModelState Range)")]
    public async Task PostTransacao_ValorZero_Retorna400()
    {
        var adulto = await CriarAdultoAsync($"Adulto ValZero {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Cat ValZero {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Despesa);

        var dto  = new CreateTransacaoRequest("Teste", 0m, ETipo.Despesa, cat.Id, adulto.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/v1/transacoes — descrição vazia retorna 400 (ModelState Required)")]
    public async Task PostTransacao_DescricaoVazia_Retorna400()
    {
        var adulto = await CriarAdultoAsync($"Adulto DescVazia {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Cat DescVazia {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Despesa);

        var dto  = new CreateTransacaoRequest("", 100m, ETipo.Despesa, cat.Id, adulto.Id, DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/v1/transacoes — pessoa inexistente retorna 400 (ArgumentException)")]
    public async Task PostTransacao_PessoaInexistente_Retorna400()
    {
        var cat  = await CriarCatAsync($"Cat PessNE {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Despesa);
        var dto  = new CreateTransacaoRequest("Teste", 100m, ETipo.Despesa,
            cat.Id, Guid.NewGuid(), DateTime.Today);
        var resp = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        // ArgumentException é capturada pelo controller → 400
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/v1/transacoes — categoria inexistente retorna 400 (ArgumentException)")]
    public async Task PostTransacao_CategoriaInexistente_Retorna400()
    {
        var adulto = await CriarAdultoAsync($"Adulto CatNE {Guid.NewGuid().ToString("N")[..6]}");
        var dto    = new CreateTransacaoRequest("Teste", 100m, ETipo.Despesa,
            Guid.NewGuid(), adulto.Id, DateTime.Today);
        var resp   = await _client.PostAsJsonAsync(ApiRoutes.Transacoes, dto);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /api/v1/transacoes/{id}
    // =========================================================================

    [Fact(DisplayName = "GET /api/v1/transacoes/{id} — id inexistente retorna 404")]
    public async Task GetTransacaoPorId_IdInexistente_Retorna404()
    {
        var resp = await _client.GetAsync($"{ApiRoutes.Transacoes}/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /api/v1/transacoes/{id} — após criar, retorna dados corretos")]
    public async Task GetTransacaoPorId_AposCriar_RetornaDadosCorretos()
    {
        var adulto = await CriarAdultoAsync($"Adulto GetTx {Guid.NewGuid().ToString("N")[..6]}");
        var cat    = await CriarCatAsync($"Sal GetTx {Guid.NewGuid().ToString("N")[..6]}", EFinalidade.Receita);

        var criado = await _client.PostAsJsonAsync(ApiRoutes.Transacoes,
            new CreateTransacaoRequest("Salário Get", 4500m, ETipo.Receita,
                cat.Id, adulto.Id, DateTime.Today));
        var tx = await criado.Content.ReadFromJsonAsync<TransacaoResponse>(JsonOptions.Default);

        var resp    = await _client.GetAsync($"{ApiRoutes.Transacoes}/{tx!.Id}");
        var obtida  = await resp.Content.ReadFromJsonAsync<TransacaoResponse>(JsonOptions.Default);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        obtida!.Descricao.Should().Be("Salário Get");
        obtida.Valor.Should().Be(4500m);
    }
}
