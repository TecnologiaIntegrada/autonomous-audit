namespace AutonomousAudit.Application.Data;

public interface ITitanSmtpSettings
{
    bool EstaConfigurado { get; }
    bool EhDespachante(string email);
    SmtpEnvioConfig ToConfig();
}
