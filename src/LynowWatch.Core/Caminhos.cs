using System.Globalization;
using System.Text.RegularExpressions;

namespace LynowWatch.Core;

/// <summary>Expansão de variáveis e marcadores e comparação de caminhos.</summary>
public static partial class Caminhos
{
    private static readonly StringComparison Comparacao =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Expande %VARIAVEIS% e "~" para a pasta do usuário.</summary>
    public static string Expandir(string caminho)
    {
        var s = Environment.ExpandEnvironmentVariables(caminho.Trim());
        if (s == "~" || s.StartsWith("~/") || s.StartsWith("~\\"))
        {
            s = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + s[1..];
        }
        return Path.GetFullPath(s);
    }

    /// <summary>
    /// Substitui os marcadores do destino. Datas vêm da última alteração do arquivo,
    /// para que "{ano}/{mes}" reflita quando o documento foi feito, não quando foi organizado.
    /// </summary>
    public static string MontarDestino(string modelo, FileInfo arquivo, string raizMonitorada)
    {
        var data = arquivo.LastWriteTime;
        var relativo = Path.GetRelativePath(raizMonitorada, arquivo.DirectoryName ?? raizMonitorada);
        if (relativo == ".")
        {
            relativo = "";
        }
        var ext = arquivo.Extension.TrimStart('.').ToLowerInvariant();
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ano"] = data.ToString("yyyy", CultureInfo.InvariantCulture),
            ["mes"] = data.ToString("MM", CultureInfo.InvariantCulture),
            ["dia"] = data.ToString("dd", CultureInfo.InvariantCulture),
            ["ext"] = string.IsNullOrEmpty(ext) ? "sem-extensao" : ext,
            ["nome"] = Path.GetFileNameWithoutExtension(arquivo.Name),
            ["relativo"] = relativo,
            ["pasta"] = new DirectoryInfo(raizMonitorada).Name,
        };
        var preenchido = Marcador().Replace(modelo, m =>
            valores.TryGetValue(m.Groups[1].Value, out var v)
                ? v
                : throw new FormatException($"Marcador desconhecido {{{m.Groups[1].Value}}} em \"{modelo}\"."));
        return Expandir(preenchido);
    }

    /// <summary>Indica se <paramref name="caminho"/> é a própria <paramref name="pasta"/> ou está dentro dela.</summary>
    public static bool EstaDentro(string caminho, string pasta)
    {
        var c = Normalizar(caminho);
        var p = Normalizar(pasta);
        return c.Equals(p, Comparacao) || c.StartsWith(p + Path.DirectorySeparatorChar, Comparacao);
    }

    /// <summary>Encontra um nome livre: "arquivo.pdf", "arquivo (2).pdf", "arquivo (3).pdf"…</summary>
    public static string NomeLivre(string caminhoDesejado)
    {
        if (!File.Exists(caminhoDesejado))
        {
            return caminhoDesejado;
        }
        var pasta = Path.GetDirectoryName(caminhoDesejado)!;
        var nome = Path.GetFileNameWithoutExtension(caminhoDesejado);
        var ext = Path.GetExtension(caminhoDesejado);
        for (var i = 2; i < 10_000; i++)
        {
            var candidato = Path.Combine(pasta, $"{nome} ({i}){ext}");
            if (!File.Exists(candidato))
            {
                return candidato;
            }
        }
        throw new IOException($"Não há nome livre para {caminhoDesejado}.");
    }

    private static string Normalizar(string caminho) =>
        Path.GetFullPath(caminho).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    [GeneratedRegex(@"\{([a-zA-Z]+)\}")]
    private static partial Regex Marcador();
}
