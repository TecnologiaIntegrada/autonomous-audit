namespace AutonomousAudit.Domain;

public static class CompraStatus
{
    public const string Rascunho = "rascunho";
    public const string Processando = "processando";
    public const string Processado = "processado";
    public const string Revisao = "revisao";
    public const string Validada = "validada";
    public const string Concluida = "concluida";
    public const string FalhaProcessamento = "falha_processamento";
}

public static class CompraAnexoOrigem
{
    public const string Pdf = "pdf";
    public const string Imagem = "imagem";
    public const string Captura = "captura";
}
