namespace AutonomousAudit.Domain;

public class CompraAnexo
{
    public Guid Id { get; private set; }
    public Guid CompraId { get; private set; }
    public int Ordem { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;
    public string Mime { get; private set; } = string.Empty;
    public string Origem { get; private set; } = CompraAnexoOrigem.Imagem;
    public string HashSha256 { get; private set; } = string.Empty;
    public string CaminhoLocal { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }

    private CompraAnexo()
    {
    }

    public static CompraAnexo Criar(
        Guid compraId,
        int ordem,
        string nomeArquivo,
        string mime,
        string origem,
        string hashSha256,
        string caminhoLocal,
        long tamanhoBytes) => new()
    {
        Id = Guid.NewGuid(),
        CompraId = compraId,
        Ordem = ordem,
        NomeArquivo = nomeArquivo,
        Mime = mime,
        Origem = origem,
        HashSha256 = hashSha256,
        CaminhoLocal = caminhoLocal,
        TamanhoBytes = tamanhoBytes,
        DataCriacao = DateTimeOffset.UtcNow
    };

    public void DefinirOrdem(int ordem) => Ordem = ordem;
}
