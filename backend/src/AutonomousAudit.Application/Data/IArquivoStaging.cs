namespace AutonomousAudit.Application.Data;

public interface IArquivoStaging
{
    Task<string> SalvarAsync(Guid transacaoId, Stream conteudo, CancellationToken cancellationToken = default);
    void Excluir(string caminhoLocal);
}
