using System.IO.Compression;

namespace LynowWatch.Core;

/// <summary>Resultado de aplicar (ou simular) uma regra em um arquivo.</summary>
public sealed record Resultado(
    DateTime Quando,
    string Pasta,
    string Regra,
    TipoAcao Acao,
    string Origem,
    string? Destino,
    string Situacao,
    string? Detalhe = null)
{
    public const string Feito = "feito";
    public const string Simulado = "simulado";
    public const string Ignorado = "ignorado";
    public const string Falhou = "falhou";
}

/// <summary>Arquivo + regra escolhida + caminho final calculado.</summary>
public sealed record Plano(PastaMonitorada Pasta, string Raiz, Regra Regra, FileInfo Arquivo, string PastaDestino);

/// <summary>
/// Aplica as regras. Não conhece FileSystemWatcher nem serviço do Windows,
/// o que permite testar tudo com pastas temporárias.
/// </summary>
public sealed class Motor
{
    private readonly Func<DateTime> _agora;

    public Motor(Func<DateTime>? agora = null)
    {
        _agora = agora ?? (() => DateTime.Now);
    }

    /// <summary>Escolhe a primeira regra que combina com o arquivo, ou null.</summary>
    public Plano? Planejar(PastaMonitorada pasta, string caminhoArquivo)
    {
        var arquivo = new FileInfo(caminhoArquivo);
        if (!arquivo.Exists || Filtros.DeveIgnorar(arquivo.Name, pasta.Ignorar))
        {
            return null;
        }
        if (arquivo.Attributes.HasFlag(FileAttributes.Hidden) || arquivo.Attributes.HasFlag(FileAttributes.System))
        {
            return null;
        }
        var raiz = Caminhos.Expandir(pasta.Caminho);
        var pastaDoArquivo = arquivo.DirectoryName!;
        if (!Caminhos.EstaDentro(pastaDoArquivo, raiz))
        {
            return null;
        }
        if (!pasta.Subpastas && !(Caminhos.EstaDentro(pastaDoArquivo, raiz) && Caminhos.EstaDentro(raiz, pastaDoArquivo)))
        {
            // Sem subpastas, só arquivos que estão diretamente na raiz.
            return null;
        }
        var agora = _agora();
        foreach (var regra in pasta.Regras)
        {
            if (Filtros.Atende(regra.Quando, arquivo, agora))
            {
                var destino = Caminhos.MontarDestino(regra.Destino, arquivo, raiz);
                return new Plano(pasta, raiz, regra, arquivo, destino);
            }
        }
        return null;
    }

    /// <summary>Executa o plano. Com <paramref name="simular"/>, só calcula e informa o que faria.</summary>
    public Resultado Executar(Plano plano, bool simular = false)
    {
        var regra = plano.Regra;
        var origem = plano.Arquivo.FullName;
        var nomeFinal = regra.Acao == TipoAcao.Compactar
            ? Path.GetFileNameWithoutExtension(plano.Arquivo.Name) + ".zip"
            : plano.Arquivo.Name;
        var desejado = Path.Combine(plano.PastaDestino, nomeFinal);

        Resultado R(string situacao, string? destino, string? detalhe = null) =>
            new(_agora(), plano.Pasta.Nome, regra.Nome, regra.Acao, origem, destino, situacao, detalhe);

        try
        {
            if (string.Equals(Path.GetFullPath(desejado), Path.GetFullPath(origem), StringComparison.OrdinalIgnoreCase))
            {
                return R(Resultado.Ignorado, desejado, "o arquivo já está no destino");
            }

            if (regra.Acao == TipoAcao.Copiar && regra.ApenasSeAlterado && File.Exists(desejado))
            {
                var existente = new FileInfo(desejado);
                if (existente.Length == plano.Arquivo.Length &&
                    Math.Abs((existente.LastWriteTimeUtc - plano.Arquivo.LastWriteTimeUtc).TotalSeconds) < 2)
                {
                    return R(Resultado.Ignorado, desejado, "cópia no destino já está atualizada");
                }
            }

            var destino = desejado;
            if (File.Exists(desejado))
            {
                switch (regra.Conflito)
                {
                    case PoliticaConflito.Ignorar:
                        return R(Resultado.Ignorado, desejado, "já existe um arquivo com esse nome no destino");
                    case PoliticaConflito.Renomear:
                        destino = Caminhos.NomeLivre(desejado);
                        break;
                    case PoliticaConflito.Substituir:
                        break;
                }
            }

            if (simular)
            {
                return R(Resultado.Simulado, destino);
            }

            Directory.CreateDirectory(plano.PastaDestino);
            var sobrescrever = regra.Conflito == PoliticaConflito.Substituir;
            switch (regra.Acao)
            {
                case TipoAcao.Mover:
                    // Entre volumes, File.Move copia e apaga; a data de alteração é preservada.
                    File.Move(origem, destino, sobrescrever);
                    break;
                case TipoAcao.Copiar:
                    File.Copy(origem, destino, sobrescrever);
                    File.SetLastWriteTimeUtc(destino, plano.Arquivo.LastWriteTimeUtc);
                    break;
                case TipoAcao.Compactar:
                    Compactar(origem, destino, sobrescrever);
                    if (!regra.ManterOriginal)
                    {
                        File.Delete(origem);
                    }
                    break;
            }
            return R(Resultado.Feito, destino);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            return R(Resultado.Falhou, desejado, e.Message);
        }
    }

    /// <summary>Lista os arquivos atuais das pastas, para varredura completa ou simulação.</summary>
    public IEnumerable<(PastaMonitorada Pasta, string Arquivo)> Varrer(ConfiguracaoWatch config)
    {
        foreach (var pasta in config.Pastas)
        {
            var raiz = Caminhos.Expandir(pasta.Caminho);
            if (!Directory.Exists(raiz))
            {
                continue;
            }
            var opcoes = new EnumerationOptions
            {
                RecurseSubdirectories = pasta.Subpastas,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            };
            foreach (var arquivo in Directory.EnumerateFiles(raiz, "*", opcoes))
            {
                yield return (pasta, arquivo);
            }
        }
    }

    private static void Compactar(string origem, string destinoZip, bool sobrescrever)
    {
        var temporario = destinoZip + ".lynowtmp";
        try
        {
            using (var zip = ZipFile.Open(temporario, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(origem, Path.GetFileName(origem), CompressionLevel.Optimal);
            }
            // Confere o zip antes de qualquer remoção do original.
            using (var conferencia = ZipFile.OpenRead(temporario))
            {
                var entrada = conferencia.Entries.Single();
                if (entrada.Length != new FileInfo(origem).Length)
                {
                    throw new IOException("o zip gerado não confere com o arquivo original");
                }
            }
            File.Move(temporario, destinoZip, sobrescrever);
        }
        finally
        {
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }
}
