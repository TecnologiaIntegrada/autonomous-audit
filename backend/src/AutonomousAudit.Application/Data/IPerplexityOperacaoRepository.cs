namespace AutonomousAudit.Application.Data;

public interface IRegistrarPerplexityUsoUsuario
{
    Task RegistrarAsync(Guid usuarioId, PerplexityAgenteResposta resposta, CancellationToken cancellationToken = default);
}

public interface IPerplexityPromptRepository
{
    Task AddAsync(Domain.PerplexityPrompt operacao, CancellationToken cancellationToken = default);
    Task<Domain.PerplexityPrompt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPerplexityArquivoRepository
{
    Task AddAsync(Domain.PerplexityArquivo operacao, CancellationToken cancellationToken = default);
    Task<Domain.PerplexityArquivo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUsuarioPerplexityUsoMensalRepository
{
    Task<Domain.UsuarioPerplexityUsoMensal?> ObterAsync(Guid usuarioId, int ano, int mes, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.UsuarioPerplexityUsoMensal registro, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
