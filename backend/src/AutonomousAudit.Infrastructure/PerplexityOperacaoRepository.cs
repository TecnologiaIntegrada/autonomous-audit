using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class PerplexityPromptRepository : IPerplexityPromptRepository
{
    private readonly AuditDbContext _db;

    public PerplexityPromptRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(PerplexityPrompt operacao, CancellationToken cancellationToken = default) =>
        await _db.PerplexityPrompts.AddAsync(operacao, cancellationToken);

    public Task<PerplexityPrompt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PerplexityPrompts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

public sealed class PerplexityArquivoRepository : IPerplexityArquivoRepository
{
    private readonly AuditDbContext _db;

    public PerplexityArquivoRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(PerplexityArquivo operacao, CancellationToken cancellationToken = default) =>
        await _db.PerplexityArquivos.AddAsync(operacao, cancellationToken);

    public Task<PerplexityArquivo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PerplexityArquivos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

public sealed class UsuarioPerplexityUsoMensalRepository : IUsuarioPerplexityUsoMensalRepository
{
    private readonly AuditDbContext _db;

    public UsuarioPerplexityUsoMensalRepository(AuditDbContext db)
    {
        _db = db;
    }

    public Task<UsuarioPerplexityUsoMensal?> ObterAsync(
        Guid usuarioId,
        int ano,
        int mes,
        CancellationToken cancellationToken = default) =>
        _db.UsuarioPerplexityUsosMensais.FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.Ano == ano && x.Mes == mes,
            cancellationToken);

    public async Task AddAsync(UsuarioPerplexityUsoMensal registro, CancellationToken cancellationToken = default) =>
        await _db.UsuarioPerplexityUsosMensais.AddAsync(registro, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
