using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;

namespace AutonomousAudit.Application;

public static class FornecedorGarantia
{
    public static async Task<Fornecedor> ResolverAsync(
        IFornecedorRepository fornecedores,
        Guid usuarioId,
        string? nome,
        string? razaoSocial,
        string? cpfCnpjDigitos,
        string? telefone,
        string? endereco,
        CancellationToken cancellationToken)
    {
        var cnpj = string.IsNullOrWhiteSpace(cpfCnpjDigitos) ? null : cpfCnpjDigitos;
        var naoIdentificado = FornecedorCorrespondencia.EhNaoIdentificado(nome);

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var porCnpj = await fornecedores.BuscarPorCpfCnpjAsync(usuarioId, cnpj, cancellationToken);
            if (porCnpj is not null)
            {
                porCnpj.MesclarCaptura(
                    naoIdentificado ? null : nome,
                    naoIdentificado ? null : TextoNormalizado.Nome(nome),
                    razaoSocial,
                    cnpj,
                    telefone,
                    endereco);
                return porCnpj;
            }
        }

        if (!naoIdentificado && !string.IsNullOrWhiteSpace(nome))
        {
            var nomeNorm = TextoNormalizado.Nome(nome);
            var porNome = await fornecedores.BuscarPorNomeAproximadoAsync(usuarioId, nomeNorm, cancellationToken);
            if (porNome is not null)
            {
                porNome.MesclarCaptura(nome, nomeNorm, razaoSocial, cnpj, telefone, endereco);
                return porNome;
            }

            var novo = Fornecedor.Criar(
                usuarioId,
                nome.Trim(),
                nomeNorm,
                razaoSocial,
                cnpj,
                telefone,
                endereco);
            await fornecedores.AddAsync(novo, cancellationToken);
            return novo;
        }

        var existente = await fornecedores.BuscarPorNomeNormalizadoAsync(
            usuarioId,
            FornecedorCorrespondencia.NomeNaoIdentificadoNormalizado,
            cancellationToken);
        if (existente is not null)
        {
            existente.MesclarCaptura(null, null, razaoSocial, cnpj, telefone, endereco);
            return existente;
        }

        var naoEncontrado = Fornecedor.Criar(
            usuarioId,
            FornecedorCorrespondencia.NomeNaoIdentificado,
            FornecedorCorrespondencia.NomeNaoIdentificadoNormalizado,
            razaoSocial,
            cnpj,
            telefone,
            endereco);
        await fornecedores.AddAsync(naoEncontrado, cancellationToken);
        return naoEncontrado;
    }
}
