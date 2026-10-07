using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class AdministradorSistemaRepository : IAdministradorSistemaRepository
{
    private readonly AuditDbContext _db;

    public AdministradorSistemaRepository(AuditDbContext db)
    {
        _db = db;
    }

    public Task<bool> EhAdministradorAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.AdministradoresSistema.AnyAsync(x => x.UsuarioId == usuarioId, cancellationToken);

    public Task<int> ContarAsync(CancellationToken cancellationToken = default) =>
        _db.AdministradoresSistema.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AdministradorSistema>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _db.AdministradoresSistema
            .AsNoTracking()
            .Include(x => x.Usuario)
            .OrderBy(x => x.DataCriacao)
            .ToListAsync(cancellationToken);

    public Task<AdministradorSistema?> GetByIdAsync(Guid idAdmin, CancellationToken cancellationToken = default) =>
        _db.AdministradoresSistema
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == idAdmin, cancellationToken);

    public Task<AdministradorSistema?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.AdministradoresSistema.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);

    public async Task AddAsync(AdministradorSistema administrador, CancellationToken cancellationToken = default) =>
        await _db.AdministradoresSistema.AddAsync(administrador, cancellationToken);

    public void Remove(AdministradorSistema administrador) =>
        _db.AdministradoresSistema.Remove(administrador);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
