using System.Text.Json;
using System.Text.Json.Serialization;

namespace LynowWatch.Core;

/// <summary>O que fazer com um arquivo que atende à regra.</summary>
public enum TipoAcao
{
    /// <summary>Move para o destino. O original deixa de existir na pasta monitorada.</summary>
    Mover,
    /// <summary>Copia para o destino, mantendo o original. Útil para backup.</summary>
    Copiar,
    /// <summary>Cria um .zip no destino. O original é removido só depois de o zip ser conferido.</summary>
    Compactar,
}

/// <summary>Como resolver quando já existe um arquivo com o mesmo nome no destino.</summary>
public enum PoliticaConflito
{
    /// <summary>Acrescenta " (2)", " (3)"… ao nome. Nunca sobrescreve.</summary>
    Renomear,
    /// <summary>Não faz nada se o destino já existir.</summary>
    Ignorar,
    /// <summary>Sobrescreve o arquivo do destino.</summary>
    Substituir,
}

/// <summary>Critérios que o arquivo precisa atender. Critérios vazios não restringem nada.</summary>
public sealed record Condicao
{
    /// <summary>Extensões aceitas, com ou sem ponto (".pdf", "dwg").</summary>
    public List<string> Extensoes { get; init; } = [];

    /// <summary>Padrões de nome com curingas: "NF-*.pdf", "relatorio_????.xlsx".</summary>
    public List<string> Padroes { get; init; } = [];

    /// <summary>Trechos que devem aparecer no nome (qualquer um basta), sem diferenciar maiúsculas.</summary>
    public List<string> NomeContem { get; init; } = [];

    public long? TamanhoMinimoKb { get; init; }
    public long? TamanhoMaximoKb { get; init; }

    /// <summary>Só age em arquivos sem alteração há pelo menos este número de dias.</summary>
    public double? IdadeMinimaDias { get; init; }
}

public sealed record Regra
{
    public string Nome { get; init; } = "";
    public Condicao Quando { get; init; } = new();
    public TipoAcao Acao { get; init; } = TipoAcao.Mover;

    /// <summary>
    /// Pasta de destino. Aceita variáveis de ambiente (%USERPROFILE%), "~" e marcadores:
    /// {ano} {mes} {dia} {ext} {nome} {relativo} {pasta}.
    /// </summary>
    public string Destino { get; init; } = "";

    public PoliticaConflito Conflito { get; init; } = PoliticaConflito.Renomear;

    /// <summary>Para Copiar: não copia de novo se o destino já tem o mesmo tamanho e data.</summary>
    public bool ApenasSeAlterado { get; init; } = true;

    /// <summary>Para Compactar: mantém o original depois de criar o zip.</summary>
    public bool ManterOriginal { get; init; }
}

public sealed record PastaMonitorada
{
    public string Nome { get; init; } = "";
    public string Caminho { get; init; } = "";
    public bool Subpastas { get; init; }

    /// <summary>Regras avaliadas em ordem; a primeira que combinar é aplicada.</summary>
    public List<Regra> Regras { get; init; } = [];

    /// <summary>Padrões adicionais de arquivos que nunca devem ser tocados.</summary>
    public List<string> Ignorar { get; init; } = [];
}

public sealed record ConfiguracaoWatch
{
    public List<PastaMonitorada> Pastas { get; init; } = [];

    /// <summary>Arquivo JSON Lines onde cada ação é registrada.</summary>
    public string Registro { get; init; } = "%ProgramData%\\LynowWatch\\registro.jsonl";

    /// <summary>Tempo sem mudança de tamanho para considerar que o arquivo terminou de ser escrito.</summary>
    public int EsperaEstabilidadeMs { get; init; } = 2000;

    /// <summary>Intervalo da varredura completa, que cobre eventos perdidos e regras por idade.</summary>
    public int VarreduraMinutos { get; init; } = 15;

    public static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly JsonSerializerOptions OpcoesJsonCompacto = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ConfiguracaoWatch Carregar(string caminho)
    {
        using var fluxo = File.OpenRead(caminho);
        return JsonSerializer.Deserialize<ConfiguracaoWatch>(fluxo, OpcoesJson)
               ?? throw new InvalidDataException($"Configuração vazia: {caminho}");
    }
}
