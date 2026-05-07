using FluentAssertions;
using MinhasFinancas.Application.DTOs;
using MinhasFinancas.Application.Services;
using MinhasFinancas.Domain.Entities;
using MinhasFinancas.Domain.Interfaces;
using NSubstitute;
using Xunit;
using static MinhasFinancas.Domain.Entities.Categoria;
using static MinhasFinancas.Domain.Entities.Transacao;

namespace MinhasFinancas.UnitTests.Domain;

/// <summary>
/// Testes das regras de negócio da entidade Transacao.
///
/// Os setters Pessoa e Categoria são "internal set", portanto só podem ser
/// atribuídos dentro do assembly MinhasFinancas.Domain/Application.
/// Testamos as regras indiretamente via TransacaoService, que é a única
/// entrada pública para criar transações e é onde os setters são chamados.
/// </summary>
public class TransacaoTests
{
    private readonly IUnitOfWork          _uow           = Substitute.For<IUnitOfWork>();
    private readonly IPessoaRepository    _pessoaRepo    = Substitute.For<IPessoaRepository>();
    private readonly ICategoriaRepository _categoriaRepo = Substitute.For<ICategoriaRepository>();
    private readonly ITransacaoRepository _transacaoRepo = Substitute.For<ITransacaoRepository>();

    public TransacaoTests()
    {
        _uow.Pessoas.Returns(_pessoaRepo);
        _uow.Categorias.Returns(_categoriaRepo);
        _uow.Transacoes.Returns(_transacaoRepo);
        _uow.SaveChangesAsync().Returns(1);
        _transacaoRepo.AddAsync(Arg.Any<Transacao>()).Returns(Task.CompletedTask);
    }

    private TransacaoService CriarService() => new(_uow);

    private static Pessoa Adulto() => new() { Nome = "Adulto", DataNascimento = DateTime.Today.AddYears(-30) };
    private static Pessoa Menor()  => new() { Nome = "Menor",  DataNascimento = DateTime.Today.AddYears(-10) };
    private static Categoria CatReceita() => new() { Descricao = "Salário",      Finalidade = EFinalidade.Receita };
    private static Categoria CatDespesa() => new() { Descricao = "Mercado",      Finalidade = EFinalidade.Despesa };
    private static Categoria CatAmbas()   => new() { Descricao = "Transferência",Finalidade = EFinalidade.Ambas  };

    private static CreateTransacaoDto DtoReceita(Guid pessoaId, Guid catId) => new()
        { Descricao = "Salário", Valor = 3000m, Tipo = ETipo.Receita, PessoaId = pessoaId, CategoriaId = catId, Data = DateTime.Today };

    private static CreateTransacaoDto DtoDespesa(Guid pessoaId, Guid catId) => new()
        { Descricao = "Lanche",  Valor = 20m,   Tipo = ETipo.Despesa, PessoaId = pessoaId, CategoriaId = catId, Data = DateTime.Today };

    // =========================================================================
    // Regra 1 — Menor de idade não pode ter Receita
    // =========================================================================

    [Fact(DisplayName = "Menor de idade + Receita → lança InvalidOperationException")]
    public async Task MenorDeIdade_ComReceita_LancaInvalidOperationException()
    {
        var menor = Menor(); var cat = CatReceita();
        _pessoaRepo.GetByIdAsync(menor.Id).Returns(menor);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(DtoReceita(menor.Id, cat.Id)));
    }

    [Fact(DisplayName = "Menor de idade + Despesa → cria com sucesso")]
    public async Task MenorDeIdade_ComDespesa_CriaComSucesso()
    {
        var menor = Menor(); var cat = CatDespesa();
        _pessoaRepo.GetByIdAsync(menor.Id).Returns(menor);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var result = await CriarService().CreateAsync(DtoDespesa(menor.Id, cat.Id));
        result.Tipo.Should().Be(ETipo.Despesa);
    }

    [Fact(DisplayName = "Adulto com 18 anos exatos + Receita → cria com sucesso")]
    public async Task AdultoDezoitoAnos_ComReceita_CriaComSucesso()
    {
        var pessoa = new Pessoa { Nome = "Dezoito", DataNascimento = DateTime.Today.AddYears(-18) };
        var cat = CatReceita();
        _pessoaRepo.GetByIdAsync(pessoa.Id).Returns(pessoa);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var result = await CriarService().CreateAsync(DtoReceita(pessoa.Id, cat.Id));
        result.Tipo.Should().Be(ETipo.Receita);
    }

    [Fact(DisplayName = "Véspera dos 18 anos + Receita → lança exceção")]
    public async Task VesperaDezoitoAnos_ComReceita_LancaExcecao()
    {
        var pessoa = new Pessoa { Nome = "Quase", DataNascimento = DateTime.Today.AddYears(-18).AddDays(1) };
        var cat = CatReceita();
        _pessoaRepo.GetByIdAsync(pessoa.Id).Returns(pessoa);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(DtoReceita(pessoa.Id, cat.Id)));
    }

    // =========================================================================
    // Regra 2 — Compatibilidade de Categoria
    // =========================================================================

    [Fact(DisplayName = "Categoria Receita + transação Despesa → lança InvalidOperationException")]
    public async Task CategoriaReceita_ComTipoDespesa_LancaExcecao()
    {
        var adulto = Adulto(); var cat = CatReceita();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(DtoDespesa(adulto.Id, cat.Id)));
    }

    [Fact(DisplayName = "Categoria Despesa + transação Receita → lança InvalidOperationException")]
    public async Task CategoriaDespesa_ComTipoReceita_LancaExcecao()
    {
        var adulto = Adulto(); var cat = CatDespesa();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(DtoReceita(adulto.Id, cat.Id)));
    }

    [Theory(DisplayName = "Categoria Ambas + qualquer tipo → cria com sucesso")]
    [InlineData(ETipo.Receita)]
    [InlineData(ETipo.Despesa)]
    public async Task CategoriaAmbas_QualquerTipo_CriaComSucesso(ETipo tipo)
    {
        var adulto = Adulto(); var cat = CatAmbas();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var dto = new CreateTransacaoDto
            { Descricao = "Transferência", Valor = 200m, Tipo = tipo, PessoaId = adulto.Id, CategoriaId = cat.Id, Data = DateTime.Today };

        var result = await CriarService().CreateAsync(dto);
        result.Tipo.Should().Be(tipo);
    }

    // =========================================================================
    // Regra 3 — Entidades não encontradas
    // =========================================================================

    [Fact(DisplayName = "Categoria inexistente → lança ArgumentException")]
    public async Task CategoriaInexistente_LancaArgumentException()
    {
        var adulto = Adulto();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Categoria?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CriarService().CreateAsync(DtoDespesa(adulto.Id, Guid.NewGuid())));
    }

    [Fact(DisplayName = "Pessoa inexistente → lança ArgumentException")]
    public async Task PessoaInexistente_LancaArgumentException()
    {
        _pessoaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Pessoa?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CriarService().CreateAsync(DtoDespesa(Guid.NewGuid(), Guid.NewGuid())));
    }
}
