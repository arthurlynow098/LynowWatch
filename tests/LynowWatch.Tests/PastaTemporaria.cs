namespace LynowWatch.Tests;

/// <summary>Pasta descartável com uma subpasta "entrada" (monitorada) e uma "saida".</summary>
internal sealed class PastaTemporaria : IDisposable
{
    public string Raiz { get; } = Path.Combine(Path.GetTempPath(), "lynowwatch-" + Guid.NewGuid().ToString("N"));
    public string Entrada => Path.Combine(Raiz, "entrada");
    public string Saida => Path.Combine(Raiz, "saida");

    public PastaTemporaria()
    {
        Directory.CreateDirectory(Entrada);
        Directory.CreateDirectory(Saida);
    }

    public string Criar(string relativo, string conteudo = "conteúdo", DateTime? alterado = null)
    {
        var caminho = Path.Combine(Entrada, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
        if (alterado is { } d)
        {
            File.SetLastWriteTime(caminho, d);
        }
        return caminho;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Raiz, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
