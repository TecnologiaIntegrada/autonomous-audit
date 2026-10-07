namespace AutonomousAudit.Domain;

public class Recurso
{
    public Guid Id { get; private set; }
    public Guid ModuloId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Chave { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string MetodoHttp { get; private set; } = string.Empty;
    public string Rota { get; private set; } = string.Empty;

    public Modulo? Modulo { get; private set; }

    private Recurso()
    {
    }

    public Recurso(
        Guid id,
        Guid moduloId,
        string codigo,
        string chave,
        string nome,
        string metodoHttp,
        string rota)
    {
        Id = id;
        ModuloId = moduloId;
        Codigo = codigo.Trim().ToLowerInvariant();
        Chave = chave.Trim().ToLowerInvariant();
        Nome = nome.Trim();
        MetodoHttp = metodoHttp.Trim().ToUpperInvariant();
        Rota = rota.Trim();
    }

    public Recurso(
        Guid moduloId,
        string codigo,
        string chave,
        string nome,
        string metodoHttp,
        string rota)
        : this(Guid.NewGuid(), moduloId, codigo, chave, nome, metodoHttp, rota)
    {
    }

    public void Atualizar(string nome, string metodoHttp, string rota)
    {
        Nome = nome.Trim();
        MetodoHttp = metodoHttp.Trim().ToUpperInvariant();
        Rota = rota.Trim();
    }

    public void DefinirModulo(Guid moduloId)
    {
        ModuloId = moduloId;
    }

    public void DefinirCodigo(string codigo)
    {
        Codigo = codigo.Trim().ToLowerInvariant();
    }

    public void DefinirChave(string chave)
    {
        Chave = chave.Trim().ToLowerInvariant();
    }

    public static Recurso Hidratar(
        Guid id,
        Guid moduloId,
        string codigo,
        string chave,
        string nome,
        string metodoHttp,
        string rota) =>
        new(id, moduloId, codigo, chave, nome, metodoHttp, rota);
}
