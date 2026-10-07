namespace AutonomousAudit.Application;

public static class TarefaLimite
{
    public static async Task ExecutarAsync(Task tarefa, TimeSpan limite, string etapa, CancellationToken cancellationToken)
    {
        try
        {
            await tarefa.WaitAsync(limite, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Timeout de {limite.TotalSeconds:0}s em {etapa}.");
        }
    }

    public static async Task<T> ExecutarAsync<T>(Task<T> tarefa, TimeSpan limite, string etapa, CancellationToken cancellationToken)
    {
        try
        {
            return await tarefa.WaitAsync(limite, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Timeout de {limite.TotalSeconds:0}s em {etapa}.");
        }
    }
}
