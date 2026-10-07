namespace AutonomousAudit.Domain;

public enum DropboxDispatchStatus
{
    Pendente = 0,
    Enviando = 1,
    Sucesso = 2,
    Falha = 3
}

public class DropboxDispatch
{
    public const int MaxTentativasPadrao = 4;

    public Guid Id { get; private set; }
    public Guid ArquivoRecebidoId { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;
    public string NomeOriginal { get; private set; } = string.Empty;
    public string CaminhoLocal { get; private set; } = string.Empty;
    public string CaminhoDropbox { get; private set; } = string.Empty;
    public string? DropboxId { get; private set; }
    public DropboxDispatchStatus Status { get; private set; }
    public int Tentativas { get; private set; }
    public int MaxTentativas { get; private set; }
    public string? UltimaMensagemErro { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? DataConclusao { get; private set; }

    public ArquivoRecebido? Arquivo { get; private set; }

    private DropboxDispatch()
    {
    }

    public DropboxDispatch(
        Guid transacaoId,
        Guid arquivoRecebidoId,
        string nomeArquivo,
        string nomeOriginal,
        string caminhoLocal,
        string caminhoDropbox,
        int maxTentativas = MaxTentativasPadrao)
    {
        Id = transacaoId;
        ArquivoRecebidoId = arquivoRecebidoId;
        NomeArquivo = nomeArquivo;
        NomeOriginal = string.IsNullOrWhiteSpace(nomeOriginal) ? nomeArquivo : nomeOriginal.Trim();
        CaminhoLocal = caminhoLocal;
        CaminhoDropbox = caminhoDropbox;
        Status = DropboxDispatchStatus.Pendente;
        Tentativas = 0;
        MaxTentativas = maxTentativas < 1 ? MaxTentativasPadrao : maxTentativas;
        DataCriacao = DateTimeOffset.UtcNow;
        DataAtualizacao = DataCriacao;
    }

    public bool EstaTerminal =>
        Status is DropboxDispatchStatus.Sucesso or DropboxDispatchStatus.Falha;

    public bool PodeTentar =>
        !EstaTerminal && Tentativas < MaxTentativas;

    public void MarcarEnviando()
    {
        Status = DropboxDispatchStatus.Enviando;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirNomeDropbox(string nomeArquivo, string caminhoDropbox)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo) || string.IsNullOrWhiteSpace(caminhoDropbox))
        {
            return;
        }

        NomeArquivo = nomeArquivo.Trim();
        CaminhoDropbox = caminhoDropbox.Trim();
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RegistrarErroPublicacao(string erro)
    {
        UltimaMensagemErro = Truncar(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
        if (Status != DropboxDispatchStatus.Sucesso)
        {
            Status = DropboxDispatchStatus.Pendente;
        }
    }

    public void RegistrarTentativaFalha(string erro)
    {
        Tentativas++;
        UltimaMensagemErro = Truncar(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
        if (Tentativas >= MaxTentativas)
        {
            Status = DropboxDispatchStatus.Falha;
            DataConclusao = DataAtualizacao;
        }
        else
        {
            Status = DropboxDispatchStatus.Pendente;
        }
    }

    public void MarcarSucesso(string dropboxId, string caminhoDropbox)
    {
        Tentativas++;
        DropboxId = dropboxId;
        CaminhoDropbox = caminhoDropbox;
        Status = DropboxDispatchStatus.Sucesso;
        UltimaMensagemErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        DataConclusao = DataAtualizacao;
    }

    private static string Truncar(string erro)
    {
        var texto = erro.Trim();
        return texto.Length <= 1000 ? texto : texto[..1000];
    }

    public static DropboxDispatch Hidratar(
        Guid id,
        Guid arquivoRecebidoId,
        string nomeArquivo,
        string nomeOriginal,
        string caminhoLocal,
        string caminhoDropbox,
        string? dropboxId,
        DropboxDispatchStatus status,
        int tentativas,
        int maxTentativas,
        string? ultimaMensagemErro,
        DateTimeOffset dataCriacao,
        DateTimeOffset dataAtualizacao,
        DateTimeOffset? dataConclusao)
    {
        return new DropboxDispatch
        {
            Id = id,
            ArquivoRecebidoId = arquivoRecebidoId,
            NomeArquivo = nomeArquivo,
            NomeOriginal = nomeOriginal ?? string.Empty,
            CaminhoLocal = caminhoLocal,
            CaminhoDropbox = caminhoDropbox,
            DropboxId = dropboxId,
            Status = status,
            Tentativas = tentativas,
            MaxTentativas = maxTentativas,
            UltimaMensagemErro = ultimaMensagemErro,
            DataCriacao = dataCriacao,
            DataAtualizacao = dataAtualizacao,
            DataConclusao = dataConclusao
        };
    }
}
