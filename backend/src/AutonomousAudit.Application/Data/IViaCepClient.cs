namespace AutonomousAudit.Application.Data;

public interface IViaCepClient
{
    Task<EnderecoConsulta?> ConsultarPorCepAsync(string cepDigitos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnderecoConsulta>> PesquisarAsync(
        string uf,
        string cidade,
        string logradouro,
        CancellationToken cancellationToken = default);
}

public sealed record EnderecoConsulta(
    string Cep,
    string Logradouro,
    string Complemento,
    string Unidade,
    string Bairro,
    string Cidade,
    string Uf,
    string Estado,
    string Regiao,
    string CodigoIbge,
    string CodigoGia,
    string Ddd,
    string CodigoSiafi,
    string Pais,
    string Origem);
