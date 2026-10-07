namespace AutonomousAudit.Domain;

public class Fornecedor
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;
    public string? RazaoSocial { get; private set; }
    public string? CpfCnpj { get; private set; }
    public string? Telefone { get; private set; }
    public string? Endereco { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    private Fornecedor()
    {
    }

    public static Fornecedor Criar(
        Guid usuarioId,
        string nome,
        string nomeNormalizado,
        string? razaoSocial,
        string? cpfCnpj,
        string? telefone,
        string? endereco)
    {
        var agora = DateTimeOffset.UtcNow;
        return new Fornecedor
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Nome = nome.Trim(),
            NomeNormalizado = nomeNormalizado,
            RazaoSocial = TextoOpcional(razaoSocial),
            CpfCnpj = TextoOpcional(cpfCnpj),
            Telefone = TextoOpcional(telefone),
            Endereco = TextoOpcional(endereco),
            DataCriacao = agora,
            DataAtualizacao = agora
        };
    }

    public void Atualizar(
        string nome,
        string nomeNormalizado,
        string? razaoSocial,
        string? cpfCnpj,
        string? telefone,
        string? endereco)
    {
        Nome = nome.Trim();
        NomeNormalizado = nomeNormalizado;
        RazaoSocial = TextoOpcional(razaoSocial);
        CpfCnpj = TextoOpcional(cpfCnpj);
        Telefone = TextoOpcional(telefone);
        Endereco = TextoOpcional(endereco);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void MesclarCaptura(
        string? nome,
        string? nomeNormalizado,
        string? razaoSocial,
        string? cpfCnpj,
        string? telefone,
        string? endereco)
    {
        if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            Nome = nome.Trim();
            NomeNormalizado = nomeNormalizado;
        }

        if (!string.IsNullOrWhiteSpace(razaoSocial))
        {
            RazaoSocial = razaoSocial.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cpfCnpj) && string.IsNullOrWhiteSpace(CpfCnpj))
        {
            CpfCnpj = cpfCnpj.Trim();
        }

        if (!string.IsNullOrWhiteSpace(telefone))
        {
            Telefone = telefone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(endereco))
        {
            Endereco = endereco.Trim();
        }

        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    private static string? TextoOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
