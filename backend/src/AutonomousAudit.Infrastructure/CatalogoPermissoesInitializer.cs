using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Infrastructure;

public static class CatalogoPermissoesInitializer
{
    public static async Task SincronizarAsync(AuditDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var definicao in CatalogoRecursos.Modulos)
        {
            var id = CatalogoRecursos.IdModulo(definicao.Codigo);
            var existente = await db.Modulos.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (existente is null)
            {
                db.Modulos.Add(new Modulo(id, definicao.Codigo, definicao.Nome, definicao.Descricao));
            }
            else
            {
                existente.Atualizar(definicao.Nome, definicao.Descricao);
            }
        }

        foreach (var definicao in CatalogoRecursos.Recursos)
        {
            var id = CatalogoRecursos.IdRecurso(definicao.Chave);
            var moduloId = CatalogoRecursos.IdModulo(definicao.ModuloCodigo);
            var existente = await db.Recursos.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existente is null)
            {
                db.Recursos.Add(new Recurso(
                    id,
                    moduloId,
                    definicao.RecursoCodigo,
                    definicao.Chave,
                    definicao.Nome,
                    definicao.MetodoHttp,
                    definicao.Rota));
            }
            else
            {
                existente.Atualizar(definicao.Nome, definicao.MetodoHttp, definicao.Rota);
            }
        }

        var chavesAtuais = CatalogoRecursos.Recursos.Select(r => r.Chave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recursosSeedObsoletos = (await db.Recursos.ToListAsync(cancellationToken))
            .Where(r => r.Id == CatalogoRecursos.IdRecurso(r.Chave) && !chavesAtuais.Contains(r.Chave))
            .ToList();
        if (recursosSeedObsoletos.Count > 0)
        {
            var ids = recursosSeedObsoletos.Select(r => r.Id).ToList();
            var permissoes = await db.UsuarioPermissoes.Where(p => ids.Contains(p.RecursoId)).ToListAsync(cancellationToken);
            db.UsuarioPermissoes.RemoveRange(permissoes);
            db.Recursos.RemoveRange(recursosSeedObsoletos);
        }

        var codigosAtuais = CatalogoRecursos.Modulos.Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modulosSeedObsoletos = (await db.Modulos.ToListAsync(cancellationToken))
            .Where(m => m.Id == CatalogoRecursos.IdModulo(m.Codigo) && !codigosAtuais.Contains(m.Codigo))
            .ToList();
        if (modulosSeedObsoletos.Count > 0)
        {
            db.Modulos.RemoveRange(modulosSeedObsoletos);
        }

        await db.SaveChangesAsync(cancellationToken);
        await ConcederNovosRecursosAosUsuariosAsync(db, cancellationToken);
    }

    private static async Task ConcederNovosRecursosAosUsuariosAsync(
        AuditDbContext db,
        CancellationToken cancellationToken)
    {
        var recursos = await db.Recursos.ToListAsync(cancellationToken);
        if (recursos.Count == 0)
        {
            return;
        }

        var usuarios = await db.Usuarios.Include(u => u.Permissoes).ToListAsync(cancellationToken);
        foreach (var usuario in usuarios)
        {
            if (usuario.Permissoes.Count == 0)
            {
                continue;
            }

            var atuais = usuario.Permissoes.Select(p => p.RecursoId).ToHashSet();
            foreach (var recurso in recursos)
            {
                if (atuais.Contains(recurso.Id))
                {
                    continue;
                }

                usuario.AdicionarPermissao(recurso.ModuloId, recurso.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AcessoBootstrap
{
    public const string AdminEmail = "admin@canada-software.com.br";
    public const string AdminNome = "Administrador";

    private readonly AuditDbContext _db;
    private readonly ISenhaHasher _senhas;
    private readonly ILogger<AcessoBootstrap> _logger;

    public AcessoBootstrap(
        AuditDbContext db,
        ISenhaHasher senhas,
        ILogger<AcessoBootstrap> logger)
    {
        _db = db;
        _senhas = senhas;
        _logger = logger;
    }

    public async Task ExecutarAsync(CancellationToken cancellationToken = default)
    {
        await CatalogoPermissoesInitializer.SincronizarAsync(_db, cancellationToken);
        await GarantirUsuarioAdminAsync(cancellationToken);
    }

    private async Task GarantirUsuarioAdminAsync(CancellationToken cancellationToken)
    {
        var senha = SenhaDoDia();
        var hash = _senhas.Hash(senha);
        var recursos = await _db.Recursos.ToListAsync(cancellationToken);
        var permissoes = recursos.Select(r => (r.ModuloId, r.Id)).ToList();

        var admin = await _db.Usuarios
            .Include(u => u.Permissoes)
            .FirstOrDefaultAsync(u => u.EmailPrincipal == AdminEmail, cancellationToken);

        if (admin is null)
        {
            admin = new Usuario(
                AdminNome,
                AdminEmail,
                hash,
                emailSecundario: "suporte.admin@canada-software.com.br",
                telefonePrincipal: "11987654321",
                telefoneSecundario: "1133334444",
                tipoDocto: "RG",
                nDocto: "123456789",
                dataEmissao: new DateOnly(2018, 3, 12),
                orgao: "SSP",
                cidade: "Sao Paulo",
                endereco: string.Empty,
                cep: string.Empty,
                uf: "SP",
                pais: "Brasil");
            admin.PreencherCadastroIncompleto();
            admin.SubstituirPermissoes(permissoes);
            await _db.Usuarios.AddAsync(admin, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Usuario admin criado {Email} com {Qtd} recursos. Senha = data do dia (ddMMyyyy) no fuso America/Sao_Paulo",
                admin.EmailPrincipal,
                permissoes.Count);
        }
        else
        {
            admin.Atualizar(
                AdminNome,
                AdminEmail,
                "suporte.admin@canada-software.com.br",
                "11987654321",
                "1133334444",
                "RG",
                "123456789",
                new DateOnly(2018, 3, 12),
                "SSP",
                "Sao Paulo",
                string.Empty,
                string.Empty,
                "SP",
                "Brasil",
                smsAuth: false,
                emailAuth: false);
            admin.DefinirSenhaHash(hash);
            admin.SubstituirPermissoes(permissoes);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Usuario admin atualizado {Email} com {Qtd} recursos. Senha = data do dia (ddMMyyyy) no fuso America/Sao_Paulo",
                admin.EmailPrincipal,
                permissoes.Count);
        }

        await GarantirAdministradorSistemaAsync(admin.Id, cancellationToken);
    }

    private async Task GarantirAdministradorSistemaAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var jaIncluido = await _db.AdministradoresSistema
            .AnyAsync(a => a.UsuarioId == usuarioId, cancellationToken);
        if (jaIncluido)
        {
            return;
        }

        await _db.AdministradoresSistema.AddAsync(new AdministradorSistema(usuarioId), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Usuario {UsuarioId} incluido em ADM_SYS", usuarioId);
    }

    internal static string SenhaDoDia()
    {
        DateTime hoje;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");
            hoje = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            hoje = DateTime.Now;
        }

        return hoje.ToString("ddMMyyyy");
    }
}
