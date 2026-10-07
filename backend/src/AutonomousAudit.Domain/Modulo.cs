namespace AutonomousAudit.Domain;

public class Modulo
{
    private readonly List<Recurso> _recursos = [];

    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;

    public IReadOnlyCollection<Recurso> Recursos => _recursos;

    private Modulo()
    {
    }

    public Modulo(Guid id, string codigo, string nome, string descricao)
    {
        Id = id;
        Codigo = codigo.Trim().ToLowerInvariant();
        Nome = nome.Trim();
        Descricao = descricao.Trim();
    }

    public Modulo(string codigo, string nome, string descricao)
        : this(Guid.NewGuid(), codigo, nome, descricao)
    {
    }

    public void Atualizar(string nome, string descricao)
    {
        Nome = nome.Trim();
        Descricao = descricao.Trim();
    }

    public void HidratarRecursos(IEnumerable<Recurso> recursos)
    {
        _recursos.Clear();
        _recursos.AddRange(recursos);
    }

    public void DefinirCodigo(string codigo)
    {
        Codigo = codigo.Trim().ToLowerInvariant();
        foreach (var recurso in _recursos)
        {
            recurso.DefinirChave($"{Codigo}.{recurso.Codigo}");
        }
    }
}
