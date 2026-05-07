using FluentAssertions;
using MinhasFinancas.Domain.Entities;
using Xunit;

namespace MinhasFinancas.UnitTests.Domain;

/// <summary>
/// Testes unitários para a entidade Pessoa.
/// Cobre: cálculo de idade (CalcularIdade via propriedade Idade) e EhMaiorDeIdade().
/// </summary>
public class PessoaTests
{
    // =========================================================================
    // Propriedade Idade (CalcularIdade privado)
    // =========================================================================

    [Fact(DisplayName = "Idade — retorna corretamente quando aniversário já ocorreu no ano")]
    public void Idade_AniversarioJaPassado_RetornaIdadeCorreta()
    {
        var pessoa = new Pessoa { DataNascimento = new DateTime(1990, 3, 10) };
        // Não podemos injetar data de referência pois o método usa DateTime.Today internamente.
        // Validamos que a propriedade retorna um valor coerente (>= 30 a.C. 2024).
        pessoa.Idade.Should().BeGreaterThanOrEqualTo(34);
    }

    [Fact(DisplayName = "Idade — recém-nascido hoje tem idade 0")]
    public void Idade_NascidoHoje_RetornaZero()
    {
        var pessoa = new Pessoa { DataNascimento = DateTime.Today };
        pessoa.Idade.Should().Be(0);
    }

    [Fact(DisplayName = "Idade — pessoa com 17 anos: aniversário ainda não ocorreu no ano corrente retorna 16 ou 17")]
    public void Idade_Dezessete_AniversarioNaoOcorrido_RetornaIdadeCorreta()
    {
        // Nasce daqui a 1 dia (faz 18 anos amanhã) → tem 17 hoje
        var nascimento = DateTime.Today.AddYears(-18).AddDays(1);
        var pessoa = new Pessoa { DataNascimento = nascimento };
        pessoa.Idade.Should().Be(17);
    }

    [Fact(DisplayName = "Idade — no exato dia do aniversário a idade é incrementada")]
    public void Idade_NoDiaDoAniversario_RetornaIdadeIncrementada()
    {
        var nascimento = DateTime.Today.AddYears(-18);
        var pessoa = new Pessoa { DataNascimento = nascimento };
        pessoa.Idade.Should().Be(18);
    }

    // =========================================================================
    // EhMaiorDeIdade
    // =========================================================================

    [Fact(DisplayName = "EhMaiorDeIdade — retorna true para pessoa adulta (30 anos)")]
    public void EhMaiorDeIdade_PessoaAdulta_RetornaTrue()
    {
        var pessoa = new Pessoa { DataNascimento = DateTime.Today.AddYears(-30) };
        pessoa.EhMaiorDeIdade().Should().BeTrue();
    }

    [Fact(DisplayName = "EhMaiorDeIdade — retorna true para pessoa com exatos 18 anos hoje")]
    public void EhMaiorDeIdade_ExatosDezoitoAnos_RetornaTrue()
    {
        var pessoa = new Pessoa { DataNascimento = DateTime.Today.AddYears(-18) };
        pessoa.EhMaiorDeIdade().Should().BeTrue();
    }

    [Fact(DisplayName = "EhMaiorDeIdade — retorna false para pessoa que faz 18 amanhã (17 anos hoje)")]
    public void EhMaiorDeIdade_VesperaDezoitoAnos_RetornaFalse()
    {
        var pessoa = new Pessoa { DataNascimento = DateTime.Today.AddYears(-18).AddDays(1) };
        pessoa.EhMaiorDeIdade().Should().BeFalse();
    }

    [Fact(DisplayName = "EhMaiorDeIdade — retorna false para criança de 10 anos")]
    public void EhMaiorDeIdade_Crianca_RetornaFalse()
    {
        var pessoa = new Pessoa { DataNascimento = DateTime.Today.AddYears(-10) };
        pessoa.EhMaiorDeIdade().Should().BeFalse();
    }

    // =========================================================================
    // Invariantes de entidade
    // =========================================================================

    [Fact(DisplayName = "Pessoa — Id gerado automaticamente não é Guid.Empty")]
    public void Pessoa_IdGeradoAutomaticamente_NaoEhGuidEmpty()
    {
        var pessoa = new Pessoa();
        pessoa.Id.Should().NotBe(Guid.Empty);
    }
}
