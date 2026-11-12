namespace LynowWatch.Core;

/// <summary>
/// Espera o arquivo "assentar" antes de mexer nele: um download ou uma cópia grande
/// dispara o evento de criação no início, não no fim.
/// </summary>
public static class Estabilidade
{
    /// <returns>true quando o tamanho ficou estável pelo intervalo e o arquivo abriu sem compartilhamento.</returns>
    public static async Task<bool> AguardarAsync(string caminho, TimeSpan intervalo, TimeSpan limite, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        long tamanhoAnterior = -1;
        while (DateTime.UtcNow - inicio < limite)
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(caminho);
            if (!info.Exists)
            {
                return false;
            }
            if (info.Length == tamanhoAnterior && PodeAbrirExclusivo(caminho))
            {
                return true;
            }
            tamanhoAnterior = info.Length;
            await Task.Delay(intervalo, ct);
        }
        return false;
    }

    internal static bool PodeAbrirExclusivo(string caminho)
    {
        try
        {
            using var _ = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
