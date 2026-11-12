using System.Text.RegularExpressions;

namespace LynowWatch.Core;

/// <summary>Decide se um arquivo atende a uma condição e se deve ser ignorado.</summary>
public static class Filtros
{
    /// <summary>
    /// Arquivos que navegadores, Office e o Windows criam durante o uso.
    /// Mexer neles quebraria downloads em andamento ou documentos abertos.
    /// </summary>
    public static readonly IReadOnlyList<string> SempreIgnorados =
    [
        "*.crdownload", "*.part", "*.partial", "*.download", "*.tmp", "*.temp",
        "~$*", ".~lock.*", "desktop.ini", "Thumbs.db", ".DS_Store", "*.lnk",
    ];

    public static bool DeveIgnorar(string nomeArquivo, IEnumerable<string> padroesExtras) =>
        SempreIgnorados.Concat(padroesExtras).Any(p => CombinaPadrao(nomeArquivo, p));

    public static bool Atende(Condicao c, FileInfo arquivo, DateTime agora)
    {
        var nome = arquivo.Name;

        if (c.Extensoes.Count > 0)
        {
            var ext = arquivo.Extension;
            if (!c.Extensoes.Any(e => string.Equals(NormalizarExtensao(e), ext, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        if (c.Padroes.Count > 0 && !c.Padroes.Any(p => CombinaPadrao(nome, p)))
        {
            return false;
        }
        if (c.NomeContem.Count > 0 && !c.NomeContem.Any(t => nome.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        var kb = arquivo.Length / 1024.0;
        if (c.TamanhoMinimoKb is { } min && kb < min)
        {
            return false;
        }
        if (c.TamanhoMaximoKb is { } max && kb > max)
        {
            return false;
        }
        if (c.IdadeMinimaDias is { } dias && (agora - arquivo.LastWriteTime).TotalDays < dias)
        {
            return false;
        }
        return true;
    }

    /// <summary>Curingas do Windows: * para qualquer sequência, ? para um caractere.</summary>
    public static bool CombinaPadrao(string nome, string padrao)
    {
        var regex = "^" + Regex.Escape(padrao).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(nome, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizarExtensao(string e) => e.StartsWith('.') ? e : "." + e;
}
