using FluentAssertions;
using MinhasFinancas.Application.DTOs;
using MinhasFinancas.Application.Services;
using MinhasFinancas.Domain.Entities;
using MinhasFinancas.Domain.Interfaces;
using MinhasFinancas.Domain.ValueObjects;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MinhasFinancas.UnitTests.Application;

/// <summary>
/// Testes unitários do PessoaService real.
///
/// Foco: CRUD, exclusão e comportamento com a entidade de domínio Pessoa.
///
/// NOTA SOBRE EXCLUSÃO EM CASCATA:
/// PessoaService.DeleteAsync apenas chama _unitOfWork.Pessoas.DeleteAsync(id)
/// + SaveChangesAsync. A exclusão em cascata de Transacoes depende 100% da
/// configuração do EF Core / banco de dados (sem DeleteBehavior.Cascade no
/// DbContext). Ver BUG-003.
/// </summary>
public class PessoaServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPessoaRepository _pessoaRepo = Substitute.For<IPessoaRepository>();

    public PessoaServiceTests()
    {
        _uow.Pessoas.Returns(_pessoaRepo);
        _uow.SaveChangesAsync().Returns(1);
    }

    private PessoaService CriarService() => new(_uow);

    // =========================================================================
    // CreateAsync
    // =========================================================================

    [Fact(DisplayName = "CreateAsync — dto válido cria e retorna PessoaDto")]
    public async Task CreateAsync_DtoValido_RetornaPessoaDto()
    {
        _pessoaRepo.AddAsync(Arg.Any<Pessoa>()).Returns(Task.CompletedTask);

        var dto    = new CreatePessoaDto { Nome = "Maria Silva", DataNascimento = new DateTime(1990, 5, 20) };
        var result = await CriarService().CreateAsync(dto);

        result.Should().NotBeNull();
        result.Nome.Should().Be("Maria Silva");
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact(DisplayName = "CreateAsync — dto nulo lança ArgumentNullException")]
    public async Task CreateAsync_DtoNulo_LancaArgumentNullException()
    {
        var act = () => CriarService().CreateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "CreateAsync — DataNascimento é preservada no DTO retornado")]
    public async Task CreateAsync_DataNascimento_EhPreservadaNoDto()
    {
        _pessoaRepo.AddAsync(Arg.Any<Pessoa>()).Returns(Task.CompletedTask);
        var nascimento = new DateTime(2005, 8, 15);
        var dto        = new CreatePessoaDto { Nome = "Jovem", DataNascimento = nascimento };

        var result = await CriarService().CreateAsync(dto);

        result.DataNascimento.Should().Be(nascimento);
    }

    // =========================================================================
    // GetByIdAsync
    // =========================================================================

    [Fact(DisplayName = "GetByIdAsync — id existente retorna PessoaDto mapeado")]
    public async Task GetByIdAsync_IdExistente_RetornaPessoaDto()
    {
        var pessoa = new Pessoa { Nome = "Carlos", DataNascimento = new DateTime(1985, 3, 22) };
        _pessoaRepo.GetByIdAsync(pessoa.Id).Returns(pessoa);

        var result = await CriarService().GetByIdAsync(pessoa.Id);

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Carlos");
        result.Id.Should().Be(pessoa.Id);
    }

    [Fact(DisplayName = "GetByIdAsync — id inexistente retorna null")]
    public async Task GetByIdAsync_IdInexistente_RetornaNull()
    {
        _pessoaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Pessoa?)null);
        var result = await CriarService().GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // =========================================================================
    // UpdateAsync
    // =========================================================================

    [Fact(DisplayName = "UpdateAsync — id existente atualiza nome e DataNascimento")]
    public async Task UpdateAsync_IdExistente_AtualizaDados()
    {
        var pessoa = new Pessoa { Nome = "Antigo Nome", DataNascimento = new DateTime(1980, 1, 1) };
        _pessoaRepo.GetByIdAsync(pessoa.Id).Returns(pessoa);
        _pessoaRepo.UpdateAsync(Arg.Any<Pessoa>()).Returns(Task.CompletedTask);

        var dto = new UpdatePessoaDto { Nome = "Novo Nome", DataNascimento = new DateTime(1981, 6, 10) };
        await CriarService().UpdateAsync(pessoa.Id, dto);

        await _pessoaRepo.Received(1).UpdateAsync(Arg.Is<Pessoa>(p =>
            p.Nome == "Novo Nome" && p.DataNascimento == new DateTime(1981, 6, 10)));
    }

    [Fact(DisplayName = "UpdateAsync — id inexistente lança KeyNotFoundException")]
    public async Task UpdateAsync_IdInexistente_LancaKeyNotFoundException()
    {
        _pessoaRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Pessoa?)null);
        var dto = new UpdatePessoaDto { Nome = "X", DataNascimento = DateTime.Today.AddYears(-20) };

        var act = () => CriarService().UpdateAsync(Guid.NewGuid(), dto);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "UpdateAsync — dto nulo lança ArgumentNullException")]
    public async Task UpdateAsync_DtoNulo_LancaArgumentNullException()
    {
        var act = () => CriarService().UpdateAsync(Guid.NewGuid(), null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // =========================================================================
    // DeleteAsync
    // =========================================================================

    [Fact(DisplayName = "DeleteAsync — chama repositório e SaveChanges")]
    public async Task DeleteAsync_IdValido_ChamaRepositorioESaveChanges()
    {
        var id = Guid.NewGuid();
        _pessoaRepo.DeleteAsync(id).Returns(Task.CompletedTask);

        await CriarService().DeleteAsync(id);

        await _pessoaRepo.Received(1).DeleteAsync(id);
        await _uow.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// ATENÇÃO: Documenta o comportamento atual do PessoaService.
    /// DeleteAsync NÃO exclui transações explicitamente — delega ao banco/EF.
    /// Se não houver DeleteBehavior.Cascade configurado, o banco pode rejeitar
    /// ou deixar transações órfãs. Ver BUG-003.
    /// </summary>
    [Fact(DisplayName = "DeleteAsync — NÃO chama ITransacaoRepository explicitamente (depende do EF cascade)")]
    public async Task DeleteAsync_NaoChamaTransacaoRepositorioExplicitamente()
    {
        var id = Guid.NewGuid();
        _pessoaRepo.DeleteAsync(id).Returns(Task.CompletedTask);

        await CriarService().DeleteAsync(id);

        // O service NÃO possui referência a ITransacaoRepository.
        // Isso é intencional para documentar que a cascata é responsabilidade do EF/banco.
        // Se o banco não tiver cascade, a exclusão falhará com erro de FK.
        await _uow.DidNotReceive().Transacoes.DeleteAsync(Arg.Any<Guid>());
    }
}
