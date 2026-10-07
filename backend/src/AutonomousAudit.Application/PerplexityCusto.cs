namespace AutonomousAudit.Application;

public sealed record PerplexityTarifaUsd(decimal InputPorMilhao, decimal OutputPorMilhao);

public sealed record PerplexityUso(
    long PromptTokens,
    long OutputTokens,
    long TotalTokens,
    decimal? CustoUsd = null);

public static class PerplexityCustoEstimado
{
    public static PerplexityTarifaUsd ResolverTarifa(string? modelo, PerplexityTarifaUsd tarifaPadrao)
    {
        var id = (modelo ?? string.Empty).Trim().ToLowerInvariant();
        if (id.Contains("luna", StringComparison.Ordinal))
        {
            return new PerplexityTarifaUsd(0.20m, 1.20m);
        }

        if (id.Contains("gpt-5.6-sol", StringComparison.Ordinal) || id.Contains("gpt-5-sol", StringComparison.Ordinal))
        {
            return new PerplexityTarifaUsd(5.00m, 30.00m);
        }

        if (id.Contains("gemini-3.1-flash-lite", StringComparison.Ordinal))
        {
            return new PerplexityTarifaUsd(0.25m, 1.50m);
        }

        return tarifaPadrao;
    }

    public static long TokensImagem(int largura, int altura)
    {
        if (largura <= 0 || altura <= 0)
        {
            return 0;
        }

        return (long)Math.Ceiling(largura * (decimal)altura / 750m);
    }

    public static decimal CalcularUsd(string? modelo, PerplexityUso uso, PerplexityTarifaUsd tarifaPadrao)
    {
        if (uso.CustoUsd is decimal informado && informado >= 0)
        {
            return informado;
        }

        if (uso.TotalTokens <= 0 && uso.PromptTokens <= 0 && uso.OutputTokens <= 0)
        {
            return 0m;
        }

        var tarifa = ResolverTarifa(modelo, tarifaPadrao);
        var input = uso.PromptTokens / 1_000_000m * tarifa.InputPorMilhao;
        var output = uso.OutputTokens / 1_000_000m * tarifa.OutputPorMilhao;
        return input + output;
    }

    public static decimal CalcularBrl(string? modelo, PerplexityUso uso, PerplexityTarifaUsd tarifaPadrao, decimal taxaUsdParaBrl)
    {
        if (taxaUsdParaBrl <= 0)
        {
            return 0m;
        }

        return Math.Round(CalcularUsd(modelo, uso, tarifaPadrao) * taxaUsdParaBrl, 6, MidpointRounding.AwayFromZero);
    }
}
