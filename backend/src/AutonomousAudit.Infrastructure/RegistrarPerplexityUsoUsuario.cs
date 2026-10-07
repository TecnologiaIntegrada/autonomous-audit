using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class RegistrarPerplexityUsoUsuario : IRegistrarPerplexityUsoUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUsuarioPerplexityUsoMensalRepository _mensal;
    private readonly PerplexityOptions _options;
    private readonly ILogger<RegistrarPerplexityUsoUsuario> _logger;

    public RegistrarPerplexityUsoUsuario(
        IUsuarioRepository usuarios,
        IUsuarioPerplexityUsoMensalRepository mensal,
        IOptions<PerplexityOptions> options,
        ILogger<RegistrarPerplexityUsoUsuario> logger)
    {
        _usuarios = usuarios;
        _mensal = mensal;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        Guid usuarioId,
        PerplexityAgenteResposta resposta,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            return;
        }

        var uso = resposta.Uso;
        if (uso.TotalTokens <= 0 && uso.PromptTokens <= 0 && uso.OutputTokens <= 0 && uso.CustoUsd is not > 0)
        {
            return;
        }

        var usuario = await _usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return;
        }

        var tarifaPadrao = new PerplexityTarifaUsd(
            _options.PrecoInputUsdPorMilhao,
            _options.PrecoOutputUsdPorMilhao);
        var custoBrl = PerplexityCustoEstimado.CalcularBrl(
            resposta.Modelo,
            uso,
            tarifaPadrao,
            _options.TaxaUsdParaBrl);
        var tokens = uso.TotalTokens > 0 ? uso.TotalTokens : uso.PromptTokens + uso.OutputTokens;
        usuario.AcumularUsoPerplexity(tokens, custoBrl);

        var (ano, mes) = PeriodoBrasil.MesVigente();
        var linha = await _mensal.ObterAsync(usuarioId, ano, mes, cancellationToken);
        if (linha is null)
        {
            linha = new UsuarioPerplexityUsoMensal(usuarioId, ano, mes);
            linha.Acumular(tokens, custoBrl);
            await _mensal.AddAsync(linha, cancellationToken);
        }
        else
        {
            linha.Acumular(tokens, custoBrl);
        }

        await _usuarios.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Perplexity uso acumulado usuario={UsuarioId} ano={Ano} mes={Mes} tokens={Tokens} custoBrl={CustoBrl:F6} modelo={Modelo}",
            usuarioId,
            ano,
            mes,
            tokens,
            custoBrl,
            resposta.Modelo);
    }
}
