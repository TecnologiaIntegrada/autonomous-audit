namespace AutonomousAudit.Domain;

public static class ReciboAnexoRegras
{
    public const long TamanhoMaximoBytes = 50L * 1024 * 1024;
    public const long LimiteUsuarioBytes = 1L * 1024 * 1024 * 1024;

    public static string? ValidarTamanho(long bytes)
    {
        if (bytes <= 0)
        {
            return "Arquivo vazio.";
        }

        return bytes > TamanhoMaximoBytes
            ? "Cada arquivo deve ter no maximo 50 MB."
            : null;
    }

    public static string? ValidarCota(long usadoBytes, long adicionalBytes)
    {
        if (usadoBytes >= LimiteUsuarioBytes || usadoBytes + adicionalBytes > LimiteUsuarioBytes)
        {
            return "Limite de 1 GB de armazenamento atingido. Exclua recibos para liberar espaco.";
        }

        return null;
    }
}
