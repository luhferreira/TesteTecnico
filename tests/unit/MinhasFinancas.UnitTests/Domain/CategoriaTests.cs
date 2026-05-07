using FluentAssertions;
using MinhasFinancas.Domain.Entities;
using Xunit;
using static MinhasFinancas.Domain.Entities.Categoria;
using static MinhasFinancas.Domain.Entities.Transacao;

namespace MinhasFinancas.UnitTests.Domain;

/// <summary>
/// Testes unitários para a entidade Categoria.
/// Foco: método PermiteTipo — compatibilidade entre EFinalidade e ETipo.
/// </summary>
public class CategoriaTests
{
    // =========================================================================
    // Categoria.EFinalidade.Receita
    // =========================================================================

    [Fact(DisplayName = "PermiteTipo — Finalidade.Receita aceita ETipo.Receita")]
    public void PermiteTipo_FinalidadeReceita_ComTipoReceita_RetornaTrue()
    {
        var categoria = new Categoria { Descricao = "Salário", Finalidade = EFinalidade.Receita };
        categoria.PermiteTipo(ETipo.Receita).Should().BeTrue();
    }

    [Fact(DisplayName = "PermiteTipo — Finalidade.Receita rejeita ETipo.Despesa")]
    public void PermiteTipo_FinalidadeReceita_ComTipoDespesa_RetornaFalse()
    {
        var categoria = new Categoria { Descricao = "Salário", Finalidade = EFinalidade.Receita };
        categoria.PermiteTipo(ETipo.Despesa).Should().BeFalse();
    }

    // =========================================================================
    // Categoria.EFinalidade.Despesa
    // =========================================================================

    [Fact(DisplayName = "PermiteTipo — Finalidade.Despesa aceita ETipo.Despesa")]
    public void PermiteTipo_FinalidadeDespesa_ComTipoDespesa_RetornaTrue()
    {
        var categoria = new Categoria { Descricao = "Mercado", Finalidade = EFinalidade.Despesa };
        categoria.PermiteTipo(ETipo.Despesa).Should().BeTrue();
    }

    [Fact(DisplayName = "PermiteTipo — Finalidade.Despesa rejeita ETipo.Receita")]
    public void PermiteTipo_FinalidadeDespesa_ComTipoReceita_RetornaFalse()
    {
        var categoria = new Categoria { Descricao = "Mercado", Finalidade = EFinalidade.Despesa };
        categoria.PermiteTipo(ETipo.Receita).Should().BeFalse();
    }

    // =========================================================================
    // Categoria.EFinalidade.Ambas
    // =========================================================================

    [Theory(DisplayName = "PermiteTipo — Finalidade.Ambas aceita qualquer ETipo")]
    [InlineData(ETipo.Receita)]
    [InlineData(ETipo.Despesa)]
    public void PermiteTipo_FinalidadeAmbas_AceitaQualquerTipo(ETipo tipo)
    {
        var categoria = new Categoria { Descricao = "Transferência", Finalidade = EFinalidade.Ambas };
        categoria.PermiteTipo(tipo).Should().BeTrue();
    }

    // =========================================================================
    // Invariantes
    // =========================================================================

    [Fact(DisplayName = "Categoria — Id gerado automaticamente não é Guid.Empty")]
    public void Categoria_IdGeradoAutomaticamente_NaoEhGuidEmpty()
    {
        var categoria = new Categoria();
        categoria.Id.Should().NotBe(Guid.Empty);
    }
}
