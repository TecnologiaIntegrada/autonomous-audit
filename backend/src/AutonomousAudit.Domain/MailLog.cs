namespace AutonomousAudit.Domain;

public enum MailLogStatus
{
    Pendente = 0,
    Processando = 1,
    Sucesso = 2,
    Falha = 3
}

public class MailLog
{
    public const int MaxTentativasPadrao = 4;

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string UsuarioToken { get; private set; } = string.Empty;
    public string De { get; private set; } = string.Empty;
    public string Para { get; private set; } = string.Empty;
    public string Assunto { get; private set; } = string.Empty;
    public string? NomeAnexo { get; private set; }
    public MailLogStatus Status { get; private set; }
    public int Tentativas { get; private set; }
    public int MaxTentativas { get; private set; }
    public string? RespostaServidor { get; private set; }
    public string? UltimaMensagemErro { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? DataEnvio { get; private set; }

    private MailLog()
    {
    }

    public MailLog(
        Guid contaId,
        Guid usuarioId,
        string usuarioToken,
        string de,
        string para,
        string assunto,
        string? nomeAnexo,
        int maxTentativas = MaxTentativasPadrao)
    {
        Id = Guid.NewGuid();
        ContaId = contaId;
        UsuarioId = usuarioId;
        UsuarioToken = string.IsNullOrWhiteSpace(usuarioToken) ? "sem-jti" : usuarioToken.Trim();
        if (UsuarioToken.Length > 64)
        {
            UsuarioToken = UsuarioToken[..64];
        }
        De = de.Trim();
        Para = para.Trim();
        Assunto = assunto.Trim();
        NomeAnexo = string.IsNullOrWhiteSpace(nomeAnexo) ? null : nomeAnexo.Trim();
        Status = MailLogStatus.Pendente;
        MaxTentativas = maxTentativas < 1 ? MaxTentativasPadrao : maxTentativas;
        DataCriacao = DateTimeOffset.UtcNow;
        DataAtualizacao = DataCriacao;
    }

    public bool EstaTerminal =>
        Status is MailLogStatus.Sucesso or MailLogStatus.Falha;

    public bool PodeTentar =>
        !EstaTerminal && Tentativas < MaxTentativas;

    public void MarcarProcessando()
    {
        Status = MailLogStatus.Processando;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RegistrarTentativaFalha(string erro, string? respostaServidor)
    {
        Tentativas++;
        UltimaMensagemErro = Truncar(erro);
        RespostaServidor = Truncar(respostaServidor);
        DataAtualizacao = DateTimeOffset.UtcNow;
        if (Tentativas >= MaxTentativas)
        {
            Status = MailLogStatus.Falha;
        }
        else
        {
            Status = MailLogStatus.Pendente;
        }
    }

    public void MarcarSucesso(string? respostaServidor)
    {
        Tentativas++;
        Status = MailLogStatus.Sucesso;
        RespostaServidor = Truncar(respostaServidor);
        UltimaMensagemErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        DataEnvio = DataAtualizacao;
    }

    public void MarcarFalha(string erro, string? respostaServidor = null)
    {
        Tentativas = MaxTentativas;
        Status = MailLogStatus.Falha;
        UltimaMensagemErro = Truncar(erro);
        RespostaServidor = Truncar(respostaServidor);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    private static string? Truncar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var valor = texto.Trim();
        return valor.Length <= 2000 ? valor : valor[..2000];
    }
}
