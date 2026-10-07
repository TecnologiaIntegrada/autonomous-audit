using AutonomousAudit.Application;
using Serilog.Core;
using Serilog.Events;

namespace AutonomousAudit.Api;

internal sealed class SeqExceptionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is null)
        {
            return;
        }

        foreach (var (chave, valor) in LogExcecao.Propriedades(logEvent.Exception))
        {
            if (valor is null)
            {
                continue;
            }

            var destructure = valor is not string && valor is not ValueType && valor is not Enum;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(chave, valor, destructure));
        }
    }
}
