using System.Collections.Concurrent;
using System.Threading.Channels;
using LynowWatch.Core;

namespace LynowWatch;

/// <summary>
/// Serviço em segundo plano: observa as pastas com FileSystemWatcher, enfileira os arquivos
/// novos e aplica as regras depois que cada arquivo termina de ser escrito.
/// Uma varredura periódica cobre eventos perdidos e regras baseadas em idade.
/// </summary>
internal sealed class Vigia(ConfiguracaoWatch config, ILogger<Vigia> log) : BackgroundService
{
    private readonly Channel<(PastaMonitorada Pasta, string Arquivo)> _fila =
        Channel.CreateBounded<(PastaMonitorada, string)>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    // Evita processar o mesmo arquivo duas vezes quando vários eventos chegam juntos.
    private readonly ConcurrentDictionary<string, byte> _emAndamento = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _observadores = [];
    private readonly Motor _motor = new();
    private readonly SemaphoreSlim _paralelo = new(4);
    private Registro _registro = null!;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _registro = new Registro(config.Registro);
        foreach (var pasta in config.Pastas)
        {
            Observar(pasta);
        }
        log.LogInformation("LynowWatch monitorando {Quantidade} pasta(s). Registro em {Registro}",
            config.Pastas.Count, _registro.Caminho);

        var varredura = VarrerPeriodicamenteAsync(ct);
        await foreach (var (pasta, arquivo) in _fila.Reader.ReadAllAsync(ct))
        {
            if (!_emAndamento.TryAdd(arquivo, 0))
            {
                continue;
            }
            _ = ProcessarAsync(pasta, arquivo, ct).ContinueWith(t => _emAndamento.TryRemove(arquivo, out _),
                TaskScheduler.Default);
        }
        await varredura;
    }

    private void Observar(PastaMonitorada pasta)
    {
        var raiz = Caminhos.Expandir(pasta.Caminho);
        var observador = new FileSystemWatcher(raiz)
        {
            IncludeSubdirectories = pasta.Subpastas,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024,
        };
        observador.Created += (_, e) => _fila.Writer.TryWrite((pasta, e.FullPath));
        // Navegadores baixam como .crdownload/.part e renomeiam no fim: o rename é o sinal de pronto.
        observador.Renamed += (_, e) => _fila.Writer.TryWrite((pasta, e.FullPath));
        observador.Error += (_, e) =>
            log.LogWarning(e.GetException(), "Eventos perdidos em {Pasta}; a próxima varredura recupera.", raiz);
        observador.EnableRaisingEvents = true;
        _observadores.Add(observador);
    }

    private async Task ProcessarAsync(PastaMonitorada pasta, string arquivo, CancellationToken ct)
    {
        // Planejar é barato; só espera estabilidade quando alguma regra se aplica.
        try
        {
            if (Directory.Exists(arquivo) || _motor.Planejar(pasta, arquivo) is null)
            {
                return;
            }
            await _paralelo.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            log.LogWarning("{Arquivo} não foi avaliado: {Erro}", arquivo, e.Message);
            return;
        }
        try
        {
            var pronto = await Estabilidade.AguardarAsync(arquivo,
                TimeSpan.FromMilliseconds(config.EsperaEstabilidadeMs), TimeSpan.FromMinutes(30), ct);
            if (!pronto)
            {
                return;
            }
            var plano = _motor.Planejar(pasta, arquivo);
            if (plano is null)
            {
                return;
            }
            var r = _motor.Executar(plano);
            _registro.Anotar(r);
            if (r.Situacao == Resultado.Falhou)
            {
                log.LogWarning("{Regra}: {Arquivo} não foi processado: {Detalhe}", r.Regra, arquivo, r.Detalhe);
            }
            else if (r.Situacao == Resultado.Feito)
            {
                log.LogInformation("{Regra}: {Acao} {Arquivo} → {Destino}", r.Regra, r.Acao, Path.GetFileName(arquivo), r.Destino);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            log.LogError(e, "Erro inesperado ao processar {Arquivo}", arquivo);
        }
        finally
        {
            _paralelo.Release();
        }
    }

    private async Task VarrerPeriodicamenteAsync(CancellationToken ct)
    {
        using var relogio = new PeriodicTimer(TimeSpan.FromMinutes(config.VarreduraMinutos));
        do
        {
            try
            {
                foreach (var item in _motor.Varrer(config))
                {
                    await _fila.Writer.WriteAsync(item, ct);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                log.LogError(e, "Falha na varredura periódica");
            }
        } while (await relogio.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    public override void Dispose()
    {
        foreach (var o in _observadores)
        {
            o.Dispose();
        }
        base.Dispose();
    }
}
