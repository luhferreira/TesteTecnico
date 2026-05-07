using FluentAssertions;
using MinhasFinancas.Domain.Entities;
using Xunit;
using static MinhasFinancas.Domain.Entities.Categoria;
using static MinhasFinancas.Domain.Entities.Transacao;

namespace MinhasFinancas.UnitTests.Domain;

/// <summary>
/// Testes unitários para a entidade Transacao.
///
/// As regras de negócio estão implementadas nos setters internos de
/// Transacao.Pessoa e Transacao.Categoria. O TransacaoService atribui
/// esses navegadores diretamente, por isso testamos o comportamento
/// real da entidade.
///
/// REGRAS TESTADAS:
///   1. Menor de idade não pode ter Receita.
///   2. Categoria incompatível com o tipo de transação deve lançar exceção.
/// </summary>
public class TransacaoTests
{
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

    private static Pessoa VesperaDezoitoAnos() => new()
    {
        Nome = "Quase Adulto",
        DataNascimento = DateTime.Today.AddYears(-18).AddDays(1) // faz 18 amanhã
    };

    private static Categoria CategoriaReceita() => new()
    {
        Descricao = "Salário",
        Finalidade = EFinalidade.Receita
    };

    private static Categoria CategoriaDespesa() => new()
    {
        Descricao = "Mercado",
        Finalidade = EFinalidade.Despesa
    };

    private static Categoria CategoriaAmbas() => new()
    {
        Descricao = "Transferência",
        Finalidade = EFinalidade.Ambas
    };

    // =========================================================================
    // Regra 1 — Menor de idade não pode ter Receita
    // =========================================================================

    [Fact(DisplayName = "Transacao.Pessoa — menor de idade + Receita lança InvalidOperationException")]
    public void SetPessoa_MenorDeIdade_ComReceita_LancaInvalidOperationException()
    {
        // Arrange
        var transacao = new Transacao
        {
            Descricao = "Mesada indevida",
            Valor = 100m,
            Tipo = ETipo.Receita
        };

        // Act
        var act = () => { transacao.Pessoa = MenorDeIdade(); };

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*18*");
    }

    [Fact(DisplayName = "Transacao.Pessoa — pessoa na véspera dos 18 anos + Receita lança exceção")]
    public void SetPessoa_VesperaDezoitoAnos_ComReceita_LancaInvalidOperationException()
    {
        var transacao = new Transacao { Descricao = "Bico", Valor = 50m, Tipo = ETipo.Receita };
        var act = () => { transacao.Pessoa = VesperaDezoitoAnos(); };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Transacao.Pessoa — adulto + Receita NÃO lança exceção")]
    public void SetPessoa_Adulto_ComReceita_NaoLancaExcecao()
    {
        var transacao = new Transacao { Descricao = "Salário", Valor = 3000m, Tipo = ETipo.Receita };
        var act = () => { transacao.Pessoa = AdultoValido(); };
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Transacao.Pessoa — menor de idade + Despesa NÃO lança exceção")]
    public void SetPessoa_MenorDeIdade_ComDespesa_NaoLancaExcecao()
    {
        var transacao = new Transacao { Descricao = "Lanche", Valor = 10m, Tipo = ETipo.Despesa };
        var act = () => { transacao.Pessoa = MenorDeIdade(); };
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Transacao.Pessoa — pessoa com exatos 18 anos hoje + Receita NÃO lança exceção")]
    public void SetPessoa_ExatosDezoitoAnos_ComReceita_NaoLancaExcecao()
    {
        var transacao = new Transacao { Descricao = "Primeiro salário", Valor = 1500m, Tipo = ETipo.Receita };
        var pessoa = new Pessoa { Nome = "Adulto Jovem", DataNascimento = DateTime.Today.AddYears(-18) };
        var act = () => { transacao.Pessoa = pessoa; };
        act.Should().NotThrow();
    }

    // =========================================================================
    // Regra 2 — Compatibilidade de Categoria
    // =========================================================================

    [Fact(DisplayName = "Transacao.Categoria — categoria Receita + transação Despesa lança InvalidOperationException")]
    public void SetCategoria_CategoriaReceita_ComTipoDespesa_LancaInvalidOperationException()
    {
        var transacao = new Transacao { Descricao = "Conta de luz", Valor = 150m, Tipo = ETipo.Despesa };
        var act = () => { transacao.Categoria = CategoriaReceita(); };
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*despesa*receita*");
    }

    [Fact(DisplayName = "Transacao.Categoria — categoria Despesa + transação Receita lança InvalidOperationException")]
    public void SetCategoria_CategoriaDespesa_ComTipoReceita_LancaInvalidOperationException()
    {
        var transacao = new Transacao { Descricao = "Freelance", Valor = 800m, Tipo = ETipo.Receita };
        var act = () => { transacao.Categoria = CategoriaDespesa(); };
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*receita*despesa*");
    }

    [Fact(DisplayName = "Transacao.Categoria — categoria Receita + transação Receita NÃO lança exceção")]
    public void SetCategoria_CategoriaReceita_ComTipoReceita_NaoLancaExcecao()
    {
        var transacao = new Transacao { Descricao = "Salário", Valor = 4000m, Tipo = ETipo.Receita };
        var act = () => { transacao.Categoria = CategoriaReceita(); };
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Transacao.Categoria — categoria Despesa + transação Despesa NÃO lança exceção")]
    public void SetCategoria_CategoriaDespesa_ComTipoDespesa_NaoLancaExcecao()
    {
        var transacao = new Transacao { Descricao = "Supermercado", Valor = 350m, Tipo = ETipo.Despesa };
        var act = () => { transacao.Categoria = CategoriaDespesa(); };
        act.Should().NotThrow();
    }

    [Theory(DisplayName = "Transacao.Categoria — categoria Ambas aceita Receita e Despesa sem exceção")]
    [InlineData(ETipo.Receita)]
    [InlineData(ETipo.Despesa)]
    public void SetCategoria_CategoriaAmbas_QualquerTipo_NaoLancaExcecao(ETipo tipo)
    {
        var transacao = new Transacao { Descricao = "Transferência", Valor = 200m, Tipo = tipo };
        var act = () => { transacao.Categoria = CategoriaAmbas(); };
        act.Should().NotThrow();
    }

    // =========================================================================
    // Associação correta de IDs
    // =========================================================================

    [Fact(DisplayName = "Transacao.Categoria — ao atribuir categoria válida, CategoriaId é atualizado")]
    public void SetCategoria_Valida_AtualizaCategoriaId()
    {
        var cat = CategoriaDespesa();
        var transacao = new Transacao { Descricao = "Mercado", Valor = 200m, Tipo = ETipo.Despesa };
        transacao.Categoria = cat;
        transacao.CategoriaId.Should().Be(cat.Id);
    }

    [Fact(DisplayName = "Transacao.Pessoa — ao atribuir pessoa válida, PessoaId é atualizado")]
    public void SetPessoa_Valida_AtualizaPessoaId()
    {
        var pessoa = AdultoValido();
        var transacao = new Transacao { Descricao = "Compra", Valor = 50m, Tipo = ETipo.Despesa };
        transacao.Pessoa = pessoa;
        transacao.PessoaId.Should().Be(pessoa.Id);
    }

    // =========================================================================
    // Invariantes
    // =========================================================================

    [Fact(DisplayName = "Transacao — Id gerado automaticamente não é Guid.Empty")]
    public void Transacao_IdGeradoAutomaticamente_NaoEhGuidEmpty()
    {
        var transacao = new Transacao();
        transacao.Id.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "Transacao — Valor zero deve ser rejeitado pela anotação Range")]
    public void Transacao_ValorZero_RangeAttrRejeita()
    {
        // A validação de Range(0.01, ...) é verificada pelo ModelState no controller.
        // Aqui documentamos o limite inferior esperado.
        var rangeAttr = typeof(Transacao)
            .GetProperty(nameof(Transacao.Valor))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.RangeAttribute>()
            .FirstOrDefault();

        rangeAttr.Should().NotBeNull();
        rangeAttr!.Minimum.Should().Be(0.01);
    }
}
