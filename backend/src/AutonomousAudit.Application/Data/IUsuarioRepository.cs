using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IUsuarioRepository
{
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    void Remove(Usuario usuario);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdComPermissoesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default);
    Task<bool> EmailEmUsoAsync(string email, Guid? ignorarId = null, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAdministradorSistemaRepository
{
    Task<bool> EhAdministradorAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<int> ContarAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdministradorSistema>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AdministradorSistema?> GetByIdAsync(Guid idAdmin, CancellationToken cancellationToken = default);
    Task<AdministradorSistema?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task AddAsync(AdministradorSistema administrador, CancellationToken cancellationToken = default);
    void Remove(AdministradorSistema administrador);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICatalogoPermissaoRepository
{
    Task<IReadOnlyList<Modulo>> ListarModulosComRecursosAsync(CancellationToken cancellationToken = default);
    Task<Modulo?> ObterModuloPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodigoModuloEmUsoAsync(string codigo, Guid? ignorarId = null, CancellationToken cancellationToken = default);
    Task AddModuloAsync(Modulo modulo, CancellationToken cancellationToken = default);
    void RemoveModulo(Modulo modulo);

    Task<IReadOnlyList<Recurso>> ListarRecursosAsync(Guid? moduloId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recurso>> ObterPorChavesAsync(IEnumerable<string> chaves, CancellationToken cancellationToken = default);
    Task<Recurso?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default);
    Task<Recurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ChaveRecursoEmUsoAsync(string chave, Guid? ignorarId = null, CancellationToken cancellationToken = default);
    Task AddRecursoAsync(Recurso recurso, CancellationToken cancellationToken = default);
    void RemoveRecurso(Recurso recurso);
    Task RemoverPermissoesDoModuloAsync(Guid moduloId, CancellationToken cancellationToken = default);
    Task RemoverPermissoesDoRecursoAsync(Guid recursoId, CancellationToken cancellationToken = default);

    Task<bool> UsuarioPossuiRecursoAsync(Guid usuarioId, string chaveRecurso, CancellationToken cancellationToken = default);
    Task SincronizarCatalogoAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ISenhaHasher
{
    string Hash(string senha);
    bool Verificar(string hash, string senha);
}

public sealed record TokenJwtEmitido(string Token, DateTimeOffset ExpiraEm);

public sealed record JwtIdentidade(Guid UsuarioId, string Jti, string? Purpose = null);

public sealed record GoogleIdentidade(string Sub, string Email, string Nome, string? Picture, bool EmailVerificado);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentidade?> ValidarAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record CadastroGoogleToken(
    string Sub,
    string Email,
    string Nome,
    string? Picture);

public interface IJwtTokenService
{
    TokenJwtEmitido Emitir(Usuario usuario);
    TokenJwtEmitido EmitirCadastroGoogle(CadastroGoogleToken identidade, int minutosValidade = 20);
    TokenJwtEmitido EmitirRelatorio(Usuario usuario, int diasValidade = 365);
    CadastroGoogleToken? LerCadastroGoogle(string token);
    Guid? LerUsuarioId(string token);
    JwtIdentidade? LerIdentidade(string token);
}

public abstract record ResultadoAcesso
{
    public static ResultadoAcesso Permitido(Guid usuarioId, string nome, string email, string tokenJti) =>
        new AcessoPermitido(usuarioId, nome, email, tokenJti);

    public static ResultadoAcesso Negado(int status, string titulo, string detalhe) =>
        new AcessoNegado(status, titulo, detalhe);
}

public sealed record AcessoPermitido(Guid UsuarioId, string Nome, string Email, string TokenJti) : ResultadoAcesso;

public sealed record AcessoNegado(int Status, string Titulo, string Detalhe) : ResultadoAcesso;

public interface IFrontUrl
{
    string BaseUrl { get; }
}

public interface IVerificadorAcesso
{
    Task<ResultadoAcesso> VerificarAsync(string? authorizationHeader, string chaveRecurso, CancellationToken cancellationToken = default);
    Task<ResultadoAcesso> VerificarAutenticadoAsync(string? authorizationHeader, CancellationToken cancellationToken = default);
}
