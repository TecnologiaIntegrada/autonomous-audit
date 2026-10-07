namespace AutonomousAudit.Domain;

public class ProdutoNome
{
    public Guid Id { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;

    private ProdutoNome()
    {
    }

    public static ProdutoNome Criar(Guid produtoId, string nome, string nomeNormalizado) => new()
    {
        Id = Guid.NewGuid(),
        ProdutoId = produtoId,
        Nome = nome.Trim(),
        NomeNormalizado = nomeNormalizado
    };
}
