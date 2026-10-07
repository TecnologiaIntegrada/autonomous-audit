using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AuditDbContext _db;

    public UsuarioRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        await _db.Usuarios.AddAsync(usuario, cancellationToken);

    public void Remove(Usuario usuario) => _db.Usuarios.Remove(usuario);

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Usuarios.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Usuario?> GetByIdComPermissoesAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Usuarios
            .Include(x => x.Permissoes)
            .ThenInclude(p => p.Recurso)
            .ThenInclude(r => r!.Modulo)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        return _db.Usuarios.FirstOrDefaultAsync(x => x.EmailPrincipal == normalizado, cancellationToken);
    }

    public Task<Usuario?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default)
    {
        var sub = googleSub.Trim();
        return _db.Usuarios.FirstOrDefaultAsync(x => x.GoogleSub == sub, cancellationToken);
    }

    public async Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Usuarios.AsNoTracking().OrderBy(x => x.Nome).ToListAsync(cancellationToken);

    public Task<bool> EmailEmUsoAsync(string email, Guid? ignorarId = null, CancellationToken cancellationToken = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        return _db.Usuarios.AnyAsync(
            x => x.EmailPrincipal == normalizado && (!ignorarId.HasValue || x.Id != ignorarId.Value),
            cancellationToken);
    }

    public Task<int> ContarAsync(CancellationToken cancellationToken = default) =>
        _db.Usuarios.CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

public sealed class CatalogoPermissaoRepository : ICatalogoPermissaoRepository
{
    private readonly AuditDbContext _db;

    public CatalogoPermissaoRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Modulo>> ListarModulosComRecursosAsync(CancellationToken cancellationToken = default) =>
        await _db.Modulos
            .AsNoTracking()
            .Include(m => m.Recursos)
            .OrderBy(m => m.Nome)
            .ToListAsync(cancellationToken);

    public Task<Modulo?> ObterModuloPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Modulos.Include(m => m.Recursos).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> CodigoModuloEmUsoAsync(string codigo, Guid? ignorarId = null, CancellationToken cancellationToken = default)
    {
        var normalizado = codigo.Trim().ToLowerInvariant();
        return _db.Modulos.AnyAsync(
            m => m.Codigo == normalizado && (!ignorarId.HasValue || m.Id != ignorarId.Value),
            cancellationToken);
    }

    public async Task AddModuloAsync(Modulo modulo, CancellationToken cancellationToken = default) =>
        await _db.Modulos.AddAsync(modulo, cancellationToken);

    public void RemoveModulo(Modulo modulo) => _db.Modulos.Remove(modulo);

    public async Task<IReadOnlyList<Recurso>> ListarRecursosAsync(Guid? moduloId, CancellationToken cancellationToken = default)
    {
        var query = _db.Recursos.AsNoTracking().Include(r => r.Modulo).AsQueryable();
        if (moduloId.HasValue)
        {
            query = query.Where(r => r.ModuloId == moduloId.Value);
        }

        return await query.OrderBy(r => r.Chave).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recurso>> ObterPorChavesAsync(
        IEnumerable<string> chaves,
        CancellationToken cancellationToken = default)
    {
        var lista = chaves.Select(c => c.Trim().ToLowerInvariant()).Where(c => c.Length > 0).Distinct().ToList();
        if (lista.Count == 0)
        {
            return [];
        }

        return await _db.Recursos
            .Where(r => lista.Contains(r.Chave))
            .ToListAsync(cancellationToken);
    }

    public Task<Recurso?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default)
    {
        var normalizado = chave.Trim().ToLowerInvariant();
        return _db.Recursos.FirstOrDefaultAsync(r => r.Chave == normalizado, cancellationToken);
    }

    public Task<Recurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Recursos.Include(r => r.Modulo).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ChaveRecursoEmUsoAsync(string chave, Guid? ignorarId = null, CancellationToken cancellationToken = default)
    {
        var normalizado = chave.Trim().ToLowerInvariant();
        return _db.Recursos.AnyAsync(
            r => r.Chave == normalizado && (!ignorarId.HasValue || r.Id != ignorarId.Value),
            cancellationToken);
    }

    public async Task AddRecursoAsync(Recurso recurso, CancellationToken cancellationToken = default) =>
        await _db.Recursos.AddAsync(recurso, cancellationToken);

    public void RemoveRecurso(Recurso recurso) => _db.Recursos.Remove(recurso);

    public async Task RemoverPermissoesDoModuloAsync(Guid moduloId, CancellationToken cancellationToken = default)
    {
        var permissoes = await _db.UsuarioPermissoes.Where(p => p.ModuloId == moduloId).ToListAsync(cancellationToken);
        _db.UsuarioPermissoes.RemoveRange(permissoes);
    }

    public async Task RemoverPermissoesDoRecursoAsync(Guid recursoId, CancellationToken cancellationToken = default)
    {
        var permissoes = await _db.UsuarioPermissoes.Where(p => p.RecursoId == recursoId).ToListAsync(cancellationToken);
        _db.UsuarioPermissoes.RemoveRange(permissoes);
    }

    public Task<bool> UsuarioPossuiRecursoAsync(
        Guid usuarioId,
        string chaveRecurso,
        CancellationToken cancellationToken = default)
    {
        var chave = chaveRecurso.Trim().ToLowerInvariant();
        return _db.UsuarioPermissoes
            .AnyAsync(
                p => p.UsuarioId == usuarioId && p.Recurso != null && p.Recurso.Chave == chave,
                cancellationToken);
    }

    public async Task SincronizarCatalogoAsync(CancellationToken cancellationToken = default)
    {
        await CatalogoPermissoesInitializer.SincronizarAsync(_db, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
