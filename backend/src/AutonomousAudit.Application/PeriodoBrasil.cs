namespace AutonomousAudit.Application;

public static class PeriodoBrasil
{
    static readonly TimeZoneInfo Fuso =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

    public static DateTimeOffset Agora() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);

    public static DateTimeOffset InicioDoDiaAtual()
    {
        var agora = Agora();
        var inicioLocal = new DateTime(agora.Year, agora.Month, agora.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(inicioLocal, Fuso.GetUtcOffset(inicioLocal)).ToUniversalTime();
    }

    public static (int Ano, int Mes) MesVigente()
    {
        var agora = Agora();
        return (agora.Year, agora.Month);
    }

    public static (DateTimeOffset Inicio, DateTimeOffset Fim) IntervaloMes(int ano, int mes)
    {
        if (mes is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes));
        }

        var inicioLocal = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fimLocalExclusivo = inicioLocal.AddMonths(1);
        var inicio = new DateTimeOffset(inicioLocal, Fuso.GetUtcOffset(inicioLocal)).ToUniversalTime();
        var fim = new DateTimeOffset(fimLocalExclusivo, Fuso.GetUtcOffset(fimLocalExclusivo)).ToUniversalTime()
            .AddTicks(-1);
        return (inicio, fim);
    }
}
