using System.IO.Compression;
using LynowWatch.Core;

namespace LynowWatch.Tests;

public class MotorTests
{
    private static readonly DateTime Agora = new(2026, 11, 12, 9, 0, 0);
    private readonly Motor _motor = new(() => Agora);

    private static PastaMonitorada Pasta(PastaTemporaria t, params Regra[] regras) => new()
    {
        Nome = "Teste",
        Caminho = t.Entrada,
        Regras = [.. regras],
    };

    [Fact]
    public void MoveParaPastaPorAnoEMesDaUltimaAlteracao()
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar("NF-1234.pdf", alterado: new DateTime(2026, 3, 8));
        var pasta = Pasta(t, new Regra
        {
            Nome = "Notas",
            Quando = new Condicao { Extensoes = ["pdf"], Padroes = ["NF-*"] },
            Destino = Path.Combine(t.Saida, "{ano}", "{mes}"),
        });

        var r = _motor.Executar(_motor.Planejar(pasta, arquivo)!);

        Assert.Equal(Resultado.Feito, r.Situacao);
        Assert.False(File.Exists(arquivo));
        Assert.True(File.Exists(Path.Combine(t.Saida, "2026", "03", "NF-1234.pdf")));
    }

    [Fact]
    public void PrimeiraRegraQueCombinaVence()
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar("foto.jpg");
        var pasta = Pasta(t,
            new Regra { Nome = "Imagens", Quando = new Condicao { Extensoes = [".jpg"] }, Destino = Path.Combine(t.Saida, "imagens") },
            new Regra { Nome = "Tudo", Destino = Path.Combine(t.Saida, "tudo") });

        Assert.Equal("Imagens", _motor.Planejar(pasta, arquivo)!.Regra.Nome);
    }

    [Fact]
    public void RenomeiaEmVezDeSobrescrever()
    {
        using var t = new PastaTemporaria();
        File.WriteAllText(Path.Combine(t.Saida, "relatorio.xlsx"), "versão antiga");
        var arquivo = t.Criar("relatorio.xlsx", "versão nova");
        var pasta = Pasta(t, new Regra { Nome = "Planilhas", Destino = t.Saida });

        var r = _motor.Executar(_motor.Planejar(pasta, arquivo)!);

        Assert.Equal(Path.Combine(t.Saida, "relatorio (2).xlsx"), r.Destino);
        Assert.Equal("versão antiga", File.ReadAllText(Path.Combine(t.Saida, "relatorio.xlsx")));
        Assert.Equal("versão nova", File.ReadAllText(r.Destino!));
    }

    [Fact]
    public void ConflitoIgnorarNaoMexeEmNada()
    {
        using var t = new PastaTemporaria();
        File.WriteAllText(Path.Combine(t.Saida, "a.txt"), "existente");
        var arquivo = t.Criar("a.txt");
        var pasta = Pasta(t, new Regra { Destino = t.Saida, Conflito = PoliticaConflito.Ignorar });

        var r = _motor.Executar(_motor.Planejar(pasta, arquivo)!);

        Assert.Equal(Resultado.Ignorado, r.Situacao);
        Assert.True(File.Exists(arquivo));
    }

    [Fact]
    public void SimulacaoNaoAlteraArquivos()
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar("doc.pdf");
        var pasta = Pasta(t, new Regra { Destino = Path.Combine(t.Saida, "pdf") });

        var r = _motor.Executar(_motor.Planejar(pasta, arquivo)!, simular: true);

        Assert.Equal(Resultado.Simulado, r.Situacao);
        Assert.True(File.Exists(arquivo));
        Assert.False(Directory.Exists(Path.Combine(t.Saida, "pdf")));
    }

    [Fact]
    public void CopiaPreservandoSubpastasENaoRepeteSeIgual()
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar(Path.Combine("obra-12", "planta.dwg"));
        var pasta = Pasta(t, new Regra
        {
            Nome = "Backup",
            Acao = TipoAcao.Copiar,
            Destino = Path.Combine(t.Saida, "{relativo}"),
        }) with { Subpastas = true };

        var primeira = _motor.Executar(_motor.Planejar(pasta, arquivo)!);
        var segunda = _motor.Executar(_motor.Planejar(pasta, arquivo)!);

        Assert.Equal(Resultado.Feito, primeira.Situacao);
        Assert.True(File.Exists(Path.Combine(t.Saida, "obra-12", "planta.dwg")));
        Assert.True(File.Exists(arquivo));
        Assert.Equal(Resultado.Ignorado, segunda.Situacao);
    }

    [Fact]
    public void CompactaConfereERemoveOriginal()
    {
        using var t = new PastaTemporaria();
        var conteudo = string.Concat(Enumerable.Repeat("nuvem de pontos ", 5000));
        var arquivo = t.Criar("scan.las", conteudo);
        var pasta = Pasta(t, new Regra { Acao = TipoAcao.Compactar, Destino = t.Saida });

        var r = _motor.Executar(_motor.Planejar(pasta, arquivo)!);

        Assert.Equal(Resultado.Feito, r.Situacao);
        Assert.False(File.Exists(arquivo));
        using var zip = ZipFile.OpenRead(Path.Combine(t.Saida, "scan.zip"));
        var entrada = Assert.Single(zip.Entries);
        using var leitor = new StreamReader(entrada.Open());
        Assert.Equal(conteudo, leitor.ReadToEnd());
        Assert.True(new FileInfo(Path.Combine(t.Saida, "scan.zip")).Length < conteudo.Length / 10);
    }

    [Fact]
    public void RespeitaIdadeMinima()
    {
        using var t = new PastaTemporaria();
        var novo = t.Criar("setup-novo.exe", alterado: Agora.AddDays(-2));
        var antigo = t.Criar("setup-antigo.exe", alterado: Agora.AddDays(-30));
        var pasta = Pasta(t, new Regra
        {
            Quando = new Condicao { Extensoes = [".exe"], IdadeMinimaDias = 14 },
            Destino = t.Saida,
        });

        Assert.Null(_motor.Planejar(pasta, novo));
        Assert.NotNull(_motor.Planejar(pasta, antigo));
    }

    [Theory]
    [InlineData("relatorio.pdf.crdownload")]
    [InlineData("video.mp4.part")]
    [InlineData("~$orcamento.xlsx")]
    [InlineData("Thumbs.db")]
    public void NuncaTocaEmArquivosTemporarios(string nome)
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar(nome);
        var pasta = Pasta(t, new Regra { Destino = t.Saida });

        Assert.Null(_motor.Planejar(pasta, arquivo));
    }

    [Fact]
    public void SemSubpastasIgnoraArquivosDeSubpastas()
    {
        using var t = new PastaTemporaria();
        var arquivo = t.Criar(Path.Combine("sub", "a.pdf"));
        var pasta = Pasta(t, new Regra { Destino = t.Saida });

        Assert.Null(_motor.Planejar(pasta, arquivo));
    }
}
