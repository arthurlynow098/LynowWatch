using LynowWatch.Core;

namespace LynowWatch;

/// <summary>Comandos de linha de comando para conferir regras antes de deixar o serviço agir.</summary>
internal static class Comandos
{
    public static void Ajuda()
    {
        Console.WriteLine("""
            LynowWatch — organiza, copia e compacta arquivos de pastas monitoradas.

            Uso:
              LynowWatch verificar  [--config arquivo.json]   confere a configuração
              LynowWatch simular    [--config arquivo.json]   mostra o que seria feito agora, sem mexer em nada
              LynowWatch executar   [--config arquivo.json]   aplica as regras uma vez nos arquivos atuais
              LynowWatch registro   [--ultimos 30]             mostra as últimas ações registradas
              LynowWatch                                        roda como serviço / em primeiro plano

            Sem --config, procura %ProgramData%\LynowWatch\lynowwatch.json e depois o arquivo ao lado do executável.
            """);
    }

    public static int Verificar(string caminhoConfig)
    {
        if (!TentarCarregar(caminhoConfig, out var config))
        {
            return 2;
        }
        var erros = Validador.Validar(config!);
        if (erros.Count == 0)
        {
            Escrever(ConsoleColor.Green, "✓ ");
            Console.WriteLine($"Configuração válida: {config!.Pastas.Count} pasta(s), {config.Pastas.Sum(p => p.Regras.Count)} regra(s).");
            foreach (var p in config.Pastas)
            {
                Console.WriteLine($"  {p.Nome}: {Caminhos.Expandir(p.Caminho)}{(p.Subpastas ? " (com subpastas)" : "")}");
            }
            return 0;
        }
        Escrever(ConsoleColor.Red, "✗ ");
        Console.WriteLine("A configuração tem problemas:");
        foreach (var e in erros)
        {
            Console.WriteLine($"  - {e}");
        }
        return 1;
    }

    public static int Varrer(string caminhoConfig, bool simular)
    {
        if (!TentarCarregar(caminhoConfig, out var config) || Verificar(caminhoConfig) != 0)
        {
            return 1;
        }
        var motor = new Motor();
        var registro = simular ? null : new Registro(config!.Registro);
        var contagem = new Dictionary<string, int>();
        Console.WriteLine();
        Console.WriteLine(simular ? "Simulação — nenhum arquivo será alterado:" : "Aplicando regras:");
        foreach (var (pasta, arquivo) in motor.Varrer(config!))
        {
            var plano = motor.Planejar(pasta, arquivo);
            if (plano is null)
            {
                continue;
            }
            var r = motor.Executar(plano, simular);
            registro?.Anotar(r);
            contagem[r.Situacao] = contagem.GetValueOrDefault(r.Situacao) + 1;
            var cor = r.Situacao switch
            {
                Resultado.Falhou => ConsoleColor.Red,
                Resultado.Ignorado => ConsoleColor.DarkGray,
                _ => ConsoleColor.Yellow,
            };
            Escrever(cor, $"  {r.Acao,-9}");
            Console.WriteLine($" {Path.GetFileName(r.Origem)}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"            → {r.Destino}{(r.Detalhe is null ? "" : $"  ({r.Detalhe})")}  [{r.Regra}]");
            Console.ResetColor();
        }
        Console.WriteLine();
        Console.WriteLine(contagem.Count == 0
            ? "Nenhum arquivo atende às regras agora."
            : string.Join(" · ", contagem.Select(c => $"{c.Value} {c.Key}")));
        return contagem.ContainsKey(Resultado.Falhou) ? 3 : 0;
    }

    public static int MostrarRegistro(string caminhoConfig, int ultimos)
    {
        if (!TentarCarregar(caminhoConfig, out var config))
        {
            return 2;
        }
        var registro = new Registro(config!.Registro);
        var linhas = registro.Ultimos(ultimos);
        if (linhas.Count == 0)
        {
            Console.WriteLine($"Nenhuma ação registrada ainda em {registro.Caminho}.");
            return 0;
        }
        foreach (var r in linhas.Reverse())
        {
            Console.WriteLine($"{r.Quando:dd/MM HH:mm:ss}  {r.Situacao,-8} {r.Acao,-9} {Path.GetFileName(r.Origem)} → {r.Destino}");
        }
        return 0;
    }

    private static bool TentarCarregar(string caminho, out ConfiguracaoWatch? config)
    {
        config = null;
        if (!File.Exists(caminho))
        {
            Escrever(ConsoleColor.Red, "✗ ");
            Console.WriteLine($"Configuração não encontrada: {caminho}");
            Console.WriteLine("  Copie exemplos\\lynowwatch.json para %ProgramData%\\LynowWatch\\ e ajuste as pastas.");
            return false;
        }
        try
        {
            config = ConfiguracaoWatch.Carregar(caminho);
            return true;
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException)
        {
            Escrever(ConsoleColor.Red, "✗ ");
            Console.WriteLine($"Não foi possível ler {caminho}: {e.Message}");
            return false;
        }
    }

    private static void Escrever(ConsoleColor cor, string texto)
    {
        Console.ForegroundColor = cor;
        Console.Write(texto);
        Console.ResetColor();
    }
}
