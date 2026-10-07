using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AutonomousAudit.Infrastructure;

public sealed class SenhaHasher : ISenhaHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string senha) => _hasher.HashPassword(new object(), senha);

    public bool Verificar(string hash, string senha) =>
        _hasher.VerifyHashedPassword(new object(), hash, senha) is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public TokenJwtEmitido Emitir(Usuario usuario)
    {
        var chave = Encoding.UTF8.GetBytes(GarantirChave());
        var expira = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.LifetimeMinutes));
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(chave), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.EmailPrincipal),
                new Claim(JwtRegisteredClaimNames.UniqueName, usuario.Nome),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            expires: expira.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenJwtEmitido(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    public TokenJwtEmitido EmitirCadastroGoogle(CadastroGoogleToken identidade, int minutosValidade = 20)
    {
        var chave = Encoding.UTF8.GetBytes(GarantirChave());
        var minutos = Math.Max(5, minutosValidade);
        var expira = DateTimeOffset.UtcNow.AddMinutes(minutos);
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(chave), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim("purpose", "google_cadastro"),
                new Claim("google_sub", identidade.Sub),
                new Claim(JwtRegisteredClaimNames.Email, identidade.Email),
                new Claim(JwtRegisteredClaimNames.UniqueName, identidade.Nome),
                new Claim("picture", identidade.Picture ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            expires: expira.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenJwtEmitido(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    public TokenJwtEmitido EmitirRelatorio(Usuario usuario, int diasValidade = 365)
    {
        var chave = Encoding.UTF8.GetBytes(GarantirChave());
        var dias = Math.Clamp(diasValidade, 1, 3650);
        var expira = DateTimeOffset.UtcNow.AddDays(dias);
        var jti = Guid.NewGuid().ToString("N");
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(chave), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim("purpose", "relatorio"),
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.EmailPrincipal),
                new Claim(JwtRegisteredClaimNames.UniqueName, usuario.Nome),
                new Claim(JwtRegisteredClaimNames.Jti, jti)
            ],
            expires: expira.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenJwtEmitido(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    public CadastroGoogleToken? LerCadastroGoogle(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(
                token,
                ParametrosValidacao(),
                out _);
            if (!string.Equals(principal.FindFirst("purpose")?.Value, "google_cadastro", StringComparison.Ordinal))
            {
                return null;
            }

            var sub = principal.FindFirst("google_sub")?.Value?.Trim();
            var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value?.Trim()
                        ?? principal.FindFirst(ClaimTypes.Email)?.Value?.Trim();
            var nome = principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value?.Trim()
                       ?? principal.FindFirst(ClaimTypes.Name)?.Value?.Trim()
                       ?? email;
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var picture = principal.FindFirst("picture")?.Value?.Trim();
            return new CadastroGoogleToken(sub, email.ToLowerInvariant(), nome ?? email, string.IsNullOrWhiteSpace(picture) ? null : picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token de cadastro Google rejeitado");
            return null;
        }
    }

    public Guid? LerUsuarioId(string token) => LerIdentidade(token)?.UsuarioId;

    public JwtIdentidade? LerIdentidade(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(
                token,
                ParametrosValidacao(),
                out _);

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sub, out var id))
            {
                return null;
            }

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value?.Trim() ?? string.Empty;
            var purpose = principal.FindFirst("purpose")?.Value?.Trim();
            if (string.Equals(purpose, "google_cadastro", StringComparison.Ordinal))
            {
                return null;
            }

            return new JwtIdentidade(id, jti, string.IsNullOrWhiteSpace(purpose) ? null : purpose);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning("JWT expirado em {ExpiraEm}: {Erro}", ex.Expires, ex.Message);
            return null;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning("JWT com assinatura invalida: {Erro}", ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT rejeitado tipo={Tipo}: {Erro}", ex.GetType().Name, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao validar JWT tipo={Tipo}: {Cadeia}", ex.GetType().Name, LogExcecao.Cadeia(ex));
            return null;
        }
    }

    private TokenValidationParameters ParametrosValidacao() =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GarantirChave())),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

    private string GarantirChave()
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey nao configurada ou com menos de 32 caracteres. Informe Jwt:SigningKey ou JWT_SIGNING_KEY.");
        }

        return _options.SigningKey;
    }
}

public sealed class VerificadorAcesso : IVerificadorAcesso
{
    private readonly IJwtTokenService _jwt;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<VerificadorAcesso> _logger;

    public VerificadorAcesso(
        IJwtTokenService jwt,
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ILogger<VerificadorAcesso> logger)
    {
        _jwt = jwt;
        _usuarios = usuarios;
        _catalogo = catalogo;
        _logger = logger;
    }

    public Task<ResultadoAcesso> VerificarAutenticadoAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken = default) =>
        IdentificarAsync(authorizationHeader, "autenticacao", cancellationToken);

    public async Task<ResultadoAcesso> VerificarAsync(
        string? authorizationHeader,
        string chaveRecurso,
        CancellationToken cancellationToken = default)
    {
        var autenticado = await IdentificarAsync(authorizationHeader, chaveRecurso, cancellationToken);
        if (autenticado is not AcessoPermitido permitido)
        {
            return autenticado;
        }

        var possui = await _catalogo.UsuarioPossuiRecursoAsync(permitido.UsuarioId, chaveRecurso, cancellationToken);
        if (!possui)
        {
            _logger.LogWarning(
                "Acesso recusado usuario={UsuarioId} email={Email} recurso={Recurso} motivo=permissao status=403",
                permitido.UsuarioId,
                permitido.Email,
                chaveRecurso);
            return ResultadoAcesso.Negado(
                403,
                "Permissao insuficiente",
                $"O usuario nao possui o recurso '{chaveRecurso}'.");
        }

        _logger.LogDebug(
            "Acesso autorizado usuario={UsuarioId} email={Email} recurso={Recurso}",
            permitido.UsuarioId,
            permitido.Email,
            chaveRecurso);
        return permitido;
    }

    private async Task<ResultadoAcesso> IdentificarAsync(
        string? authorizationHeader,
        string contexto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Acesso recusado recurso={Recurso} motivo=sem_bearer headerPresente={HeaderPresente}",
                contexto,
                !string.IsNullOrWhiteSpace(authorizationHeader));
            return ResultadoAcesso.Negado(401, "Nao autorizado", "Informe o JWT no header Authorization: Bearer {token}.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Acesso recusado recurso={Recurso} motivo=token_vazio", contexto);
            return ResultadoAcesso.Negado(401, "Nao autorizado", "Token JWT ausente.");
        }

        JwtIdentidade? identidade;
        try
        {
            identidade = _jwt.LerIdentidade(token);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "JWT nao configurado");
            return ResultadoAcesso.Negado(500, "Configuracao invalida", ex.Message);
        }

        if (identidade is null)
        {
            _logger.LogWarning("JWT invalido ou expirado recurso={Recurso}", contexto);
            return ResultadoAcesso.Negado(401, "Nao autorizado", "Token JWT invalido ou expirado.");
        }

        var usuario = await _usuarios.GetByIdAsync(identidade.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            _logger.LogWarning("JWT de usuario inexistente {UsuarioId}", identidade.UsuarioId);
            return ResultadoAcesso.Negado(401, "Nao autorizado", "Usuario do token nao encontrado.");
        }

        if (string.Equals(identidade.Purpose, "relatorio", StringComparison.Ordinal))
        {
            var permitidoRelatorio =
                string.Equals(contexto, RecursoChaves.RelatorioRead, StringComparison.OrdinalIgnoreCase);
            if (!permitidoRelatorio)
            {
                return ResultadoAcesso.Negado(
                    403,
                    "Permissao insuficiente",
                    "O token de relatorio so acessa o GET /v1/relatorios/itens do proprio usuario (json, csv ou xlsx).");
            }

            if (!usuario.ApiTokenConfere(identidade.Jti))
            {
                return ResultadoAcesso.Negado(
                    401,
                    "Nao autorizado",
                    "Token de relatorio revogado. Gere outro em Configuracoes.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(identidade.Purpose))
        {
            return ResultadoAcesso.Negado(401, "Nao autorizado", "Token JWT invalido para esta rota.");
        }

        return ResultadoAcesso.Permitido(usuario.Id, usuario.Nome, usuario.EmailPrincipal, identidade.Jti);
    }
}
