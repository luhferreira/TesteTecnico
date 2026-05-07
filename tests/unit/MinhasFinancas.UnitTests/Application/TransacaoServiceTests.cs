using FluentAssertions;
using MinhasFinancas.Application.DTOs;
using MinhasFinancas.Application.Services;
using MinhasFinancas.Domain.Entities;
using MinhasFinancas.Domain.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static MinhasFinancas.Domain.Entities.Categoria;
using static MinhasFinancas.Domain.Entities.Transacao;

namespace MinhasFinancas.UnitTests.Application;

/// <summary>
/// Testes unitários do TransacaoService real.
///
/// O TransacaoService delega as regras de negócio aos setters internos de
/// Transacao (Pessoa e Categoria), que lançam InvalidOperationException.
/// O controller captura apenas ArgumentException — portanto InvalidOperationException
/// escala para o ExceptionMiddleware (500).
///
/// BUG DOCUMENTADO: O controller só trata ArgumentException, deixando
/// InvalidOperationException retornar 500 em vez de 422/400.
/// Ver: docs/BUG-001-menor-idade-receita.md e BUG-002-categoria-incompativel.md
/// </summary>
public class TransacaoServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPessoaRepository _pessoaRepo = Substitute.For<IPessoaRepository>();
    private readonly ICategoriaRepository _categoriaRepo = Substitute.For<ICategoriaRepository>();
    private readonly ITransacaoRepository _transacaoRepo = Substitute.For<ITransacaoRepository>();

    public TransacaoServiceTests()
    {
        _uow.Pessoas.Returns(_pessoaRepo);
        _uow.Categorias.Returns(_categoriaRepo);
        _uow.Transacoes.Returns(_transacaoRepo);
        _uow.SaveChangesAsync().Returns(1);
    }

    private TransacaoService CriarService() => new(_uow);

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Pessoa AdultoValido() => new()
    {
        Nome = "Adulto",
        DataNascimento = DateTime.Today.AddYears(-30)
    };

    private static Pessoa MenorDeIdade() => new()
    {
        Nome = "Menor",
        DataNascimento = DateTime.Today.AddYears(-10)
    };

    private static Categoria CatReceita() => new() { Descricao = "Salário", Finalidade = EFinalidade.Receita };
    private static Categoria CatDespesa() => new() { Descricao = "Mercado", Finalidade = EFinalidade.Despesa };
    private static Categoria CatAmbas()   => new() { Descricao = "Transferência", Finalidade = EFinalidade.Ambas };

    private CreateTransacaoDto DtoReceita(Guid pessoaId, Guid catId) => new()
    {
        Descricao = "Salário",
        Valor     = 3000m,
        Tipo      = ETipo.Receita,
        PessoaId  = pessoaId,
        CategoriaId = catId,
        Data      = DateTime.Today
    };

    private CreateTransacaoDto DtoDespesa(Guid pessoaId, Guid catId) => new()
    {
        Descricao = "Lanche",
        Valor     = 20m,
        Tipo      = ETipo.Despesa,
        PessoaId  = pessoaId,
        CategoriaId = catId,
        Data      = DateTime.Today
    };

    // =========================================================================
    // Regra 1 — Menor de idade não pode ter Receita
    // =========================================================================

    [Fact(DisplayName = "CreateAsync — menor de idade + Receita lança InvalidOperationException")]
    public async Task CreateAsync_MenorDeIdade_ComReceita_LancaInvalidOperationException()
    {
        // Arrange
        var menor = MenorDeIdade();
        var cat   = CatReceita();
        _pessoaRepo.GetByIdAsync(menor.Id).Returns(menor);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var svc = CriarService();
        var dto = DtoReceita(menor.Id, cat.Id);

        // Act
        var act = () => svc.CreateAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*18*");
    }

    [Fact(DisplayName = "CreateAsync — menor de idade + Despesa cria transação com sucesso")]
    public async Task CreateAsync_MenorDeIdade_ComDespesa_CriaComSucesso()
    {
        // Arrange
        var menor = MenorDeIdade();
        var cat   = CatDespesa();
        _pessoaRepo.GetByIdAsync(menor.Id).Returns(menor);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);
        _transacaoRepo.AddAsync(Arg.Any<Transacao>()).Returns(Task.CompletedTask);

        var svc    = CriarService();
        var dto    = DtoDespesa(menor.Id, cat.Id);

        // Act
        var result = await svc.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Tipo.Should().Be(ETipo.Despesa);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact(DisplayName = "CreateAsync — adulto + Receita cria transação com sucesso")]
    public async Task CreateAsync_Adulto_ComReceita_CriaComSucesso()
    {
        var adulto = AdultoValido();
        var cat    = CatReceita();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);
        _transacaoRepo.AddAsync(Arg.Any<Transacao>()).Returns(Task.CompletedTask);

        var result = await CriarService().CreateAsync(DtoReceita(adulto.Id, cat.Id));

        result.Tipo.Should().Be(ETipo.Receita);
        result.Valor.Should().Be(3000m);
    }

    [Fact(DisplayName = "CreateAsync — pessoa na véspera dos 18 anos + Receita lança exceção")]
    public async Task CreateAsync_VesperaDezoitoAnos_ComReceita_LancaExcecao()
    {
        var quaseAdulto = new Pessoa
        {
            Nome = "Quase Adulto",
            DataNascimento = DateTime.Today.AddYears(-18).AddDays(1)
        };
        var cat = CatReceita();
        _pessoaRepo.GetByIdAsync(quaseAdulto.Id).Returns(quaseAdulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(DtoReceita(quaseAdulto.Id, cat.Id)));
    }

    // =========================================================================
    // Regra 2 — Compatibilidade de Categoria
    // =========================================================================

    [Fact(DisplayName = "CreateAsync — Categoria Receita + Tipo Despesa lança InvalidOperationException")]
    public async Task CreateAsync_CategoriaReceita_ComTipoDespesa_LancaInvalidOperationException()
    {
        var adulto = AdultoValido();
        var cat    = CatReceita();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var dto = DtoDespesa(adulto.Id, cat.Id); // tipo Despesa mas categoria Receita

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(dto));
    }

    [Fact(DisplayName = "CreateAsync — Categoria Despesa + Tipo Receita lança InvalidOperationException")]
    public async Task CreateAsync_CategoriaDespesa_ComTipoReceita_LancaInvalidOperationException()
    {
        var adulto = AdultoValido();
        var cat    = CatDespesa();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);

        var dto = DtoReceita(adulto.Id, cat.Id); // tipo Receita mas categoria Despesa

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarService().CreateAsync(dto));
    }

    [Theory(DisplayName = "CreateAsync — Categoria Ambas aceita Receita e Despesa")]
    [InlineData(ETipo.Receita)]
    [InlineData(ETipo.Despesa)]
    public async Task CreateAsync_CategoriaAmbas_QualquerTipo_CriaComSucesso(ETipo tipo)
    {
        var adulto = AdultoValido();
        var cat    = CatAmbas();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(cat.Id).Returns(cat);
        _transacaoRepo.AddAsync(Arg.Any<Transacao>()).Returns(Task.CompletedTask);

        var dto = new CreateTransacaoDto
        {
            Descricao   = "Transferência",
            Valor       = 100m,
            Tipo        = tipo,
            PessoaId    = adulto.Id,
            CategoriaId = cat.Id,
            Data        = DateTime.Today
        };

        var result = await CriarService().CreateAsync(dto);
        result.Tipo.Should().Be(tipo);
    }

    // =========================================================================
    // Entidades não encontradas
    // =========================================================================

    [Fact(DisplayName = "CreateAsync — categoria não encontrada lança ArgumentException")]
    public async Task CreateAsync_CategoriaNaoEncontrada_LancaArgumentException()
    {
        var adulto = AdultoValido();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Categoria?)null);

        var dto = DtoDespesa(adulto.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(() => CriarService().CreateAsync(dto));
    }

    [Fact(DisplayName = "CreateAsync — pessoa não encontrada lança ArgumentException")]
    public async Task CreateAsync_PessoaNaoEncontrada_LancaArgumentException()
    {
        _pessoaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Pessoa?)null);

        var dto = DtoDespesa(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(() => CriarService().CreateAsync(dto));
    }

    // =========================================================================
    // Ordem de validação — Categoria é verificada ANTES de Pessoa no service
    // =========================================================================

    [Fact(DisplayName = "CreateAsync — categoria verificada primeiro: categoria ausente lança antes de validar pessoa")]
    public async Task CreateAsync_OrdemValidacao_CategoriaAntesDeVerificarPessoa()
    {
        // Categoria não existe, Pessoa existe
        var adulto = AdultoValido();
        _pessoaRepo.GetByIdAsync(adulto.Id).Returns(adulto);
        _categoriaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Categoria?)null);

        var dto = DtoDespesa(adulto.Id, Guid.NewGuid());

        // Deve lançar ArgumentException (categoria), não chegar à validação de Pessoa
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CriarService().CreateAsync(dto));
        ex.ParamName.Should().Be("dto.CategoriaId");
    }
}
