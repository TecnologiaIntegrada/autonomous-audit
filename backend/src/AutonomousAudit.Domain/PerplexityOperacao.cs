namespace AutonomousAudit.Domain;

public enum PerplexityOperacaoStatus
{
    Pendente = 0,
    Processando = 1,
    Sucesso = 2,
    Falha = 3
}

public class PerplexityPrompt
{
    public const int MaxTentativasPadrao = 4;

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string? Modelo { get; private set; }
    public string? MimeType { get; private set; }
    public string? CaminhoLocal { get; private set; }
    public PerplexityOperacaoStatus Status { get; private set; }
    public int Tentativas { get; private set; }
    public int MaxTentativas { get; private set; }
    public string? UltimaMensagemErro { get; private set; }
    public string? ModeloResposta { get; private set; }
    public string? TextoResposta { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? DataConclusao { get; private set; }

    private PerplexityPrompt()
    {
    }

    public PerplexityPrompt(
        Guid usuarioId,
        string prompt,
        string? modelo,
        string? mimeType,
        string? caminhoLocal,
        int maxTentativas = MaxTentativasPadrao)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Prompt = prompt?.Trim() ?? string.Empty;
        Modelo = string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();
        MimeType = string.IsNullOrWhiteSpace(mimeType) ? null : mimeType.Trim();
        CaminhoLocal = string.IsNullOrWhiteSpace(caminhoLocal) ? null : caminhoLocal;
        Status = PerplexityOperacaoStatus.Pendente;
        MaxTentativas = maxTentativas < 1 ? MaxTentativasPadrao : maxTentativas;
        DataCriacao = DateTimeOffset.UtcNow;
        DataAtualizacao = DataCriacao;
    }

    public void DefinirCaminhoLocal(string caminhoLocal)
    {
        CaminhoLocal = caminhoLocal;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool EstaTerminal =>
        Status is PerplexityOperacaoStatus.Sucesso or PerplexityOperacaoStatus.Falha;

    public bool PodeTentar =>
        !EstaTerminal && Tentativas < MaxTentativas;

    public void MarcarProcessando()
    {
        Status = PerplexityOperacaoStatus.Processando;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RegistrarTentativaFalha(string erro)
    {
        Tentativas++;
        UltimaMensagemErro = Truncar(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
        if (Tentativas >= MaxTentativas)
        {
            Status = PerplexityOperacaoStatus.Falha;
            DataConclusao = DataAtualizacao;
        }
        else
        {
            Status = PerplexityOperacaoStatus.Pendente;
        }
    }

    public void MarcarSucesso(string modeloResposta, string texto)
    {
        Tentativas++;
        ModeloResposta = modeloResposta;
        TextoResposta = texto;
        Status = PerplexityOperacaoStatus.Sucesso;
        UltimaMensagemErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        DataConclusao = DataAtualizacao;
    }

    private static string Truncar(string erro)
    {
        var texto = erro.Trim();
        return texto.Length <= 1000 ? texto : texto[..1000];
    }
}

public class PerplexityArquivo
{
    public const int MaxTentativasPadrao = 4;

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid ArquivoRecebidoId { get; private set; }
    public Guid DropboxDispatchId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string? Modelo { get; private set; }
    public string? MimeType { get; private set; }
    public string NomeOriginal { get; private set; } = string.Empty;
    public string NomeArquivo { get; private set; } = string.Empty;
    public string CaminhoLocal { get; private set; } = string.Empty;
    public string CaminhoDropbox { get; private set; } = string.Empty;
    public bool DropboxInscrito { get; private set; }
    public PerplexityOperacaoStatus Status { get; private set; }
    public int Tentativas { get; private set; }
    public int MaxTentativas { get; private set; }
    public string? UltimaMensagemErro { get; private set; }
    public string? ModeloResposta { get; private set; }
    public string? TextoResposta { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? DataConclusao { get; private set; }

    private PerplexityArquivo()
    {
    }

    public PerplexityArquivo(
        Guid usuarioId,
        Guid arquivoRecebidoId,
        Guid dropboxDispatchId,
        string prompt,
        string? modelo,
        string? mimeType,
        string nomeOriginal,
        string nomeArquivo,
        string caminhoLocal,
        string caminhoDropbox,
        int maxTentativas = MaxTentativasPadrao)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        ArquivoRecebidoId = arquivoRecebidoId;
        DropboxDispatchId = dropboxDispatchId;
        Prompt = prompt?.Trim() ?? string.Empty;
        Modelo = string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim();
        MimeType = string.IsNullOrWhiteSpace(mimeType) ? null : mimeType.Trim();
        NomeOriginal = nomeOriginal;
        NomeArquivo = nomeArquivo;
        CaminhoLocal = caminhoLocal;
        CaminhoDropbox = caminhoDropbox;
        Status = PerplexityOperacaoStatus.Pendente;
        MaxTentativas = maxTentativas < 1 ? MaxTentativasPadrao : maxTentativas;
        DataCriacao = DateTimeOffset.UtcNow;
        DataAtualizacao = DataCriacao;
    }

    public bool EstaTerminal =>
        Status is PerplexityOperacaoStatus.Sucesso or PerplexityOperacaoStatus.Falha;

    public bool PodeTentar =>
        !EstaTerminal && Tentativas < MaxTentativas;

    public void MarcarProcessando()
    {
        Status = PerplexityOperacaoStatus.Processando;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void MarcarDropboxInscrito()
    {
        DropboxInscrito = true;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RegistrarTentativaFalha(string erro)
    {
        Tentativas++;
        UltimaMensagemErro = Truncar(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
        if (Tentativas >= MaxTentativas)
        {
            Status = PerplexityOperacaoStatus.Falha;
            DataConclusao = DataAtualizacao;
        }
        else
        {
            Status = PerplexityOperacaoStatus.Pendente;
        }
    }

    public void MarcarSucesso(string modeloResposta, string texto)
    {
        Tentativas++;
        ModeloResposta = modeloResposta;
        TextoResposta = texto;
        Status = PerplexityOperacaoStatus.Sucesso;
        UltimaMensagemErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        DataConclusao = DataAtualizacao;
    }

    private static string Truncar(string erro)
    {
        var texto = erro.Trim();
        return texto.Length <= 1000 ? texto : texto[..1000];
    }
}
