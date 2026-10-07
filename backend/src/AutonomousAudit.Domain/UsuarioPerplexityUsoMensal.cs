namespace AutonomousAudit.Domain;

public class UsuarioPerplexityUsoMensal
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public long Tokens { get; private set; }
    public decimal CustoBrl { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    private UsuarioPerplexityUsoMensal()
    {
    }

    public UsuarioPerplexityUsoMensal(Guid usuarioId, int ano, int mes)
    {
        if (ano < 2000 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano));
        }

        if (mes is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes));
        }

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Ano = ano;
        Mes = mes;
        Tokens = 0;
        CustoBrl = 0;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void Acumular(long tokens, decimal custoBrl)
    {
        if (tokens > 0)
        {
            Tokens += tokens;
        }

        CustoBrl += custoBrl;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }
}
