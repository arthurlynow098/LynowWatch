using LynowWatch.Core;

namespace LynowWatch.Tests;

public class ConfiguracaoTests
{
    [Theory]
    [InlineData("NF-2026-001.pdf", "NF-*.pdf", true)]
    [InlineData("nf-2026-001.PDF", "NF-*.pdf", true)]
    [InlineData("relatorio_2026.xlsx", "relatorio_????.xlsx", true)]
    [InlineData("relatorio_26.xlsx", "relatorio_????.xlsx", false)]
    [InlineData("a+b(1).txt", "a+b(*).txt", true)]
    public void CuringasComoNoWindows(string nome, string padrao, bool esperado) =>
        Assert.Equal(esperado, Filtros.CombinaPadrao(nome, padrao));

    [Fact]
    public void LeExemploComComentariosEEnumsEmTexto()
    {
        var config = ConfiguracaoWatch.Carregar(Path.Combine(AppContext.BaseDirectory, "lynowwatch.json"));

        Assert.Equal(2, config.Pastas.Count);
        var backup = config.Pastas[1].Regras[0];
        Assert.Equal(TipoAcao.Copiar, backup.Acao);
        Assert.Equal(PoliticaConflito.Substituir, backup.Conflito);
        Assert.Equal(14d, config.Pastas[0].Regras[2].Quando.IdadeMinimaDias);
    }

    [Fact]
    public void ImpedeCicloComDestinoDentroDaPastaComSubpastas()
    {
        using var t = new PastaTemporaria();
        var config = new ConfiguracaoWatch
        {
            Pastas =
            [
                new PastaMonitorada
                {
                    Nome = "Entrada",
                    Caminho = t.Entrada,
                    Subpastas = true,
                    Regras = [new Regra { Nome = "Organizar", Destino = Path.Combine(t.Entrada, "organizado", "{ano}") }],
                },
            ],
        };

        var erro = Assert.Single(Validador.Validar(config));
        Assert.Contains("dentro da pasta monitorada", erro);
    }

    [Fact]
    public void PermiteDestinoNaSubpastaQuandoNaoMonitoraSubpastas()
    {
        using var t = new PastaTemporaria();
        var config = new ConfiguracaoWatch
        {
            Pastas =
            [
                new PastaMonitorada
                {
                    Nome = "Entrada",
                    Caminho = t.Entrada,
                    Regras = [new Regra { Nome = "Organizar", Destino = Path.Combine(t.Entrada, "organizado", "{ano}") }],
                },
            ],
        };

        Assert.Empty(Validador.Validar(config));
    }

    [Fact]
    public void ApontaMarcadorDesconhecidoEPastasSobrepostas()
    {
        using var t = new PastaTemporaria();
        var config = new ConfiguracaoWatch
        {
            Pastas =
            [
                new PastaMonitorada { Nome = "A", Caminho = t.Raiz, Regras = [new Regra { Destino = Path.Combine(t.Saida, "{cliente}") }] },
                new PastaMonitorada { Nome = "B", Caminho = t.Entrada, Regras = [new Regra { Destino = t.Saida }] },
            ],
        };

        var erros = Validador.Validar(config);

        Assert.Contains(erros, e => e.Contains("{cliente}"));
        Assert.Contains(erros, e => e.Contains("se sobrepõem"));
    }

    [Fact]
    public void ExpandeVariaveisDeAmbiente()
    {
        Environment.SetEnvironmentVariable("LYNOW_TESTE", Path.GetTempPath());
        var caminho = Caminhos.Expandir(Path.Combine("%LYNOW_TESTE%", "x"));
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "x")), caminho);
    }
}
