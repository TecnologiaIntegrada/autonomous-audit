using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class TitanSmtpSettings : ITitanSmtpSettings
{
    private readonly TitanSmtpOptions _options;

    public TitanSmtpSettings(IOptions<TitanSmtpOptions> options)
    {
        _options = options.Value;
    }

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(_options.Host)
        && !string.IsNullOrWhiteSpace(_options.User)
        && !string.IsNullOrWhiteSpace(_options.Password);

    public bool EhDespachante(string email)
    {
        if (!EstaConfigurado || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var alvo = email.Trim();
        return alvo.Equals(_options.User, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrWhiteSpace(_options.From)
                   && alvo.Equals(_options.From, StringComparison.OrdinalIgnoreCase));
    }

    public SmtpEnvioConfig ToConfig() =>
        new(
            _options.Host,
            _options.Port <= 0 ? 465 : _options.Port,
            _options.User,
            _options.Password,
            string.IsNullOrWhiteSpace(_options.From) ? _options.User : _options.From,
            string.IsNullOrWhiteSpace(_options.TlsMode) ? "ssl" : _options.TlsMode);
}
