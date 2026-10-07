namespace AutonomousAudit.Application.Data;

public interface ISegredoProtector
{
    string Cifrar(string textoClaro);
    string Decifrar(string textoArmazenado);
}
