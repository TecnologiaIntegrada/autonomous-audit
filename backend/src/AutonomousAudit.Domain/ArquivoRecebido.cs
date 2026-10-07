namespace AutonomousAudit.Domain;

public class ArquivoRecebido
{
    public Guid Id { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;
    public decimal TamanhoMb { get; private set; }
    public DateTimeOffset DataHora { get; private set; }
    public string UsuarioRemetente { get; private set; } = string.Empty;

    private ArquivoRecebido()
    {
    }

    public ArquivoRecebido(
        string nomeArquivo,
        decimal tamanhoMb,
        DateTimeOffset dataHora,
        string usuarioRemetente)
    {
        Id = Guid.NewGuid();
        NomeArquivo = nomeArquivo;
        TamanhoMb = tamanhoMb;
        DataHora = dataHora;
        UsuarioRemetente = usuarioRemetente;
    }
}
