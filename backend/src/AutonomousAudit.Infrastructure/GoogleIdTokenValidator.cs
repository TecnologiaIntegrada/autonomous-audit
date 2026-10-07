using AutonomousAudit.Application.Data;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleIdTokenValidator> _logger;

    public GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options, ILogger<GoogleIdTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleIdentidade?> ValidarAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _logger.LogError("Google:ClientId nao configurado. Informe Google:ClientId ou GOOGLE_CLIENT_ID.");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId],
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5)
                });

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleIdentidade(
                payload.Subject,
                payload.Email.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name.Trim(),
                payload.Picture,
                payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(
                "id_token Google invalido: {Erro} tamanho={Tamanho} audienceEsperada={Audience}",
                ex.Message,
                idToken.Length,
                _options.ClientId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao validar id_token Google");
            return null;
        }
    }
}
