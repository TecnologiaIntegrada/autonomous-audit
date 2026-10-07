using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;

namespace AutonomousAudit.Application;

public static class ComprasEscopo
{
    public static async Task<EscopoDono> CriarAsync(
        IAdministradorSistemaRepository administradores,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var admin = await administradores.EhAdministradorAsync(usuarioId, cancellationToken);
        return new EscopoDono(usuarioId, admin);
    }

    public static bool PodeAcessar(EscopoDono escopo, Guid donoId) =>
        escopo.EhAdministrador || escopo.UsuarioId == donoId;

    public static string ChaveDuplicidade(
        string? cpfCnpj,
        string? nome,
        string? numeroRecibo,
        DateTimeOffset? dataCompra,
        decimal? total)
    {
        var fornecedor = TextoNormalizado.Digitos(cpfCnpj) ?? TextoNormalizado.Nome(nome);
        var data = dataCompra?.UtcDateTime.ToString("yyyy-MM-dd") ?? "";
        var valor = total?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "";
        var numero = (numeroRecibo ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(fornecedor) && string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(data))
        {
            return string.Empty;
        }

        return $"{fornecedor}|{numero}|{data}|{valor}";
    }
}
