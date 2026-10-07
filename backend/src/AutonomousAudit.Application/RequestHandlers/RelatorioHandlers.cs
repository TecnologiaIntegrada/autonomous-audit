using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using MediatR;

namespace AutonomousAudit.Application.RequestHandlers;

public record RelatorioItensQuery(
    Guid UsuarioId,
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    Guid? FornecedorId,
    int Limite) : IQuery<RelatorioItensResult>;

public record RelatorioItensResult(IReadOnlyList<RelatorioItemLinha> Itens, int Limite);

public sealed class RelatorioItensHandler : IRequestHandler<RelatorioItensQuery, RelatorioItensResult>
{
    public const int LimiteMaximo = 10000;
    public const int PeriodoMaximoDias = 93;

    private readonly ICompraRepository _compras;

    public RelatorioItensHandler(ICompraRepository compras)
    {
        _compras = compras;
    }

    public static string? ValidarPeriodo(DateTimeOffset inicio, DateTimeOffset fim)
    {
        if (inicio > fim)
        {
            return "O inicio do periodo nao pode ser posterior ao fim.";
        }

        if (fim - inicio > TimeSpan.FromDays(PeriodoMaximoDias))
        {
            return "O periodo nao pode ser maior que 3 meses.";
        }

        return null;
    }

    public async Task<RelatorioItensResult> Handle(RelatorioItensQuery request, CancellationToken cancellationToken)
    {
        var limite = Math.Clamp(request.Limite <= 0 ? LimiteMaximo : request.Limite, 1, LimiteMaximo);
        var itens = await _compras.RelatorioItensAsync(
            new EscopoDono(request.UsuarioId, EhAdministrador: false),
            request.Inicio,
            request.Fim,
            request.FornecedorId,
            limite,
            cancellationToken);
        return new RelatorioItensResult(itens, limite);
    }
}
