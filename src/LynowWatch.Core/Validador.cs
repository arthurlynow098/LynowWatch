namespace LynowWatch.Core;

/// <summary>Confere a configuração antes de começar a monitorar.</summary>
public static class Validador
{
    public static readonly IReadOnlyList<string> MarcadoresValidos = ["ano", "mes", "dia", "ext", "nome", "relativo", "pasta"];

    public static IReadOnlyList<string> Validar(ConfiguracaoWatch config)
    {
        var erros = new List<string>();
        if (config.Pastas.Count == 0)
        {
            erros.Add("Nenhuma pasta configurada em \"Pastas\".");
        }
        if (config.EsperaEstabilidadeMs < 200)
        {
            erros.Add("\"EsperaEstabilidadeMs\" deve ser de pelo menos 200 ms.");
        }
        if (config.VarreduraMinutos < 1)
        {
            erros.Add("\"VarreduraMinutos\" deve ser de pelo menos 1 minuto.");
        }

        var raizes = new List<(string Nome, string Caminho)>();
        foreach (var pasta in config.Pastas)
        {
            var nome = string.IsNullOrWhiteSpace(pasta.Nome) ? pasta.Caminho : pasta.Nome;
            if (string.IsNullOrWhiteSpace(pasta.Caminho))
            {
                erros.Add($"Pasta \"{nome}\": informe o \"Caminho\".");
                continue;
            }
            string raiz;
            try
            {
                raiz = Caminhos.Expandir(pasta.Caminho);
            }
            catch (Exception e)
            {
                erros.Add($"Pasta \"{nome}\": caminho inválido ({e.Message}).");
                continue;
            }
            if (!Directory.Exists(raiz))
            {
                erros.Add($"Pasta \"{nome}\": {raiz} não existe.");
            }
            raizes.Add((nome, raiz));

            if (pasta.Regras.Count == 0)
            {
                erros.Add($"Pasta \"{nome}\": nenhuma regra.");
            }
            foreach (var regra in pasta.Regras)
            {
                var r = string.IsNullOrWhiteSpace(regra.Nome) ? "(sem nome)" : regra.Nome;
                if (string.IsNullOrWhiteSpace(regra.Destino))
                {
                    erros.Add($"Regra \"{r}\" em \"{nome}\": informe o \"Destino\".");
                    continue;
                }
                var desconhecidos = System.Text.RegularExpressions.Regex.Matches(regra.Destino, @"\{([^}]*)\}")
                    .Select(m => m.Groups[1].Value)
                    .Where(m => !MarcadoresValidos.Contains(m, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (desconhecidos.Count > 0)
                {
                    erros.Add($"Regra \"{r}\" em \"{nome}\": marcador desconhecido {{{desconhecidos[0]}}}. " +
                              $"Use {string.Join(", ", MarcadoresValidos.Select(m => "{" + m + "}"))}.");
                    continue;
                }
                // Só a parte antes do primeiro marcador é fixa; é ela que define onde o arquivo vai parar.
                var fixo = regra.Destino.Split('{')[0];
                if (string.IsNullOrWhiteSpace(fixo))
                {
                    erros.Add($"Regra \"{r}\" em \"{nome}\": o destino precisa começar por uma pasta fixa.");
                    continue;
                }
                var destino = Caminhos.Expandir(fixo);
                if (pasta.Subpastas && Caminhos.EstaDentro(destino, raiz))
                {
                    // Com subpastas, o arquivo movido seria visto de novo e processado em ciclo.
                    erros.Add($"Regra \"{r}\" em \"{nome}\": o destino fica dentro da pasta monitorada com \"Subpastas\": true. " +
                              "Use um destino fora dela ou desative as subpastas.");
                }
                if (regra.Quando.IdadeMinimaDias is < 0)
                {
                    erros.Add($"Regra \"{r}\" em \"{nome}\": \"IdadeMinimaDias\" não pode ser negativa.");
                }
            }
        }

        for (var i = 0; i < raizes.Count; i++)
        {
            for (var j = i + 1; j < raizes.Count; j++)
            {
                if (Caminhos.EstaDentro(raizes[i].Caminho, raizes[j].Caminho) ||
                    Caminhos.EstaDentro(raizes[j].Caminho, raizes[i].Caminho))
                {
                    erros.Add($"As pastas \"{raizes[i].Nome}\" e \"{raizes[j].Nome}\" se sobrepõem.");
                }
            }
        }
        return erros;
    }
}
