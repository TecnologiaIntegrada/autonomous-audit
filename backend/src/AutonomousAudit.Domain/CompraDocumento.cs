namespace AutonomousAudit.Domain;

public class CompraDocumento
{
    public Guid Id { get; private set; }
    public Guid CompraId { get; private set; }
    public string DropboxPath { get; private set; } = string.Empty;
    public string? DropboxId { get; private set; }
    public Guid? DropboxDispatchId { get; private set; }
    public string Origem { get; private set; } = CompraAnexoOrigem.Pdf;
    public int Ordem { get; private set; }
    public string HashSha256 { get; private set; } = string.Empty;
    public string? CaminhoLocalPdf { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    private CompraDocumento()
    {
    }

    public static CompraDocumento Criar(
        Guid compraId,
        string dropboxPath,
        string origem,
        string hashSha256,
        string? caminhoLocalPdf) => new()
    {
        Id = Guid.NewGuid(),
        CompraId = compraId,
        DropboxPath = dropboxPath,
        Origem = origem,
        Ordem = 1,
        HashSha256 = hashSha256,
        CaminhoLocalPdf = caminhoLocalPdf,
        DataCriacao = DateTimeOffset.UtcNow,
        DataAtualizacao = DateTimeOffset.UtcNow
    };

    public void RegistrarDropbox(string path, string? dropboxId)
    {
        DropboxPath = path;
        DropboxId = dropboxId;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void MoverPara(string path)
    {
        DropboxPath = path;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }
}
