using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class ArquivoStagingDisco : IArquivoStaging
{
    private readonly string _raiz;
    private readonly ILogger<ArquivoStagingDisco> _logger;

    public ArquivoStagingDisco(IOptions<NatsOptions> options, ILogger<ArquivoStagingDisco> logger)
    {
        _logger = logger;
        _raiz = string.IsNullOrWhiteSpace(options.Value.StagingPath)
            ? Path.Combine(Path.GetTempPath(), "autonomousaudit-dropbox")
            : options.Value.StagingPath;
        Directory.CreateDirectory(_raiz);
    }

    public async Task<string> SalvarAsync(Guid transacaoId, Stream conteudo, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_raiz);
        var caminho = Path.Combine(_raiz, transacaoId.ToString("N"));
        try
        {
            await using var disco = new FileStream(caminho, FileMode.Create, FileAccess.Write, FileShare.None);
            await conteudo.CopyToAsync(disco, cancellationToken);
            await disco.FlushAsync(cancellationToken);
            _logger.LogInformation(
                "Staging salvo transacao={TransacaoId} caminho={Caminho} bytes={Bytes} raiz={Raiz}",
                transacaoId,
                caminho,
                disco.Length,
                _raiz);
            return caminho;
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(caminho))
                {
                    File.Delete(caminho);
                }
            }
            catch (Exception excluir)
            {
                _logger.LogWarning(excluir, "Nao foi possivel excluir staging incompleto {Caminho}", caminho);
            }

            _logger.LogError(
                ex,
                "Falha ao gravar staging transacao={TransacaoId} caminho={Caminho} raiz={Raiz} tipo={Tipo} cadeia={Cadeia}",
                transacaoId,
                caminho,
                _raiz,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw;
        }
    }

    public void Excluir(string caminhoLocal)
    {
        if (string.IsNullOrWhiteSpace(caminhoLocal) || !File.Exists(caminhoLocal))
        {
            return;
        }

        try
        {
            File.Delete(caminhoLocal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel excluir staging {Caminho}", caminhoLocal);
        }
    }
}
