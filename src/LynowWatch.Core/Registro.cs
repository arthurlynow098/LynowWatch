using System.Text.Json;

namespace LynowWatch.Core;

/// <summary>
/// Diário das ações em JSON Lines: uma linha por arquivo, fácil de ler,
/// filtrar com qualquer ferramenta e abrir no Excel via Power Query.
/// </summary>
public sealed class Registro
{
    private readonly string _caminho;
    private readonly object _trava = new();

    public Registro(string caminho)
    {
        _caminho = Caminhos.Expandir(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);
    }

    public string Caminho => _caminho;

    public void Anotar(Resultado r)
    {
        var linha = JsonSerializer.Serialize(r, ConfiguracaoWatch.OpcoesJsonCompacto);
        lock (_trava)
        {
            File.AppendAllText(_caminho, linha + Environment.NewLine);
        }
    }

    public IReadOnlyList<Resultado> Ultimos(int quantidade)
    {
        if (!File.Exists(_caminho))
        {
            return [];
        }
        lock (_trava)
        {
            return File.ReadLines(_caminho)
                .Reverse()
                .Take(quantidade)
                .Select(l => JsonSerializer.Deserialize<Resultado>(l, ConfiguracaoWatch.OpcoesJsonCompacto)!)
                .ToList();
        }
    }
}
