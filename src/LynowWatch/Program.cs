using LynowWatch;
using LynowWatch.Core;

var comando = args.FirstOrDefault()?.ToLowerInvariant() ?? "servico";
var caminhoConfig = Opcao(args, "--config") ?? LocalizarConfiguracao();

switch (comando)
{
    case "verificar":
        return Comandos.Verificar(caminhoConfig);
    case "simular":
        return Comandos.Varrer(caminhoConfig, simular: true);
    case "executar":
        return Comandos.Varrer(caminhoConfig, simular: false);
    case "registro":
        return Comandos.MostrarRegistro(caminhoConfig, int.TryParse(Opcao(args, "--ultimos"), out var n) ? n : 30);
    case "ajuda" or "--help" or "-h" or "/?":
        Comandos.Ajuda();
        return 0;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "LynowWatch");
builder.Services.AddSingleton(_ => CarregarValidada(caminhoConfig));
builder.Services.AddHostedService<Vigia>();
builder.Build().Run();
return 0;

static ConfiguracaoWatch CarregarValidada(string caminho)
{
    var config = ConfiguracaoWatch.Carregar(caminho);
    var erros = Validador.Validar(config);
    if (erros.Count > 0)
    {
        throw new InvalidOperationException("Configuração inválida:\n- " + string.Join("\n- ", erros));
    }
    return config;
}

static string? Opcao(string[] args, string nome)
{
    var i = Array.FindIndex(args, a => a.Equals(nome, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

/// <summary>Procura em %ProgramData%\LynowWatch e, depois, ao lado do executável.</summary>
static string LocalizarConfiguracao()
{
    var programData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LynowWatch", "lynowwatch.json");
    return File.Exists(programData) ? programData : Path.Combine(AppContext.BaseDirectory, "lynowwatch.json");
}
