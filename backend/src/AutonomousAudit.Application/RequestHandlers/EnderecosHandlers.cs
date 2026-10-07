using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ConsultarEnderecoPorCepQuery(string Cep) : IQuery<ConsultarEnderecoPorCepResult>;

public abstract record ConsultarEnderecoPorCepResult
{
    public static ConsultarEnderecoPorCepResult Ok(EnderecoConsulta endereco) => new ConsultarEnderecoPorCepOk(endereco);
    public static ConsultarEnderecoPorCepResult BadRequest(string message) => new ConsultarEnderecoPorCepBadRequest(message);
    public static ConsultarEnderecoPorCepResult NotFound(string message) => new ConsultarEnderecoPorCepNotFound(message);
    public static ConsultarEnderecoPorCepResult Fail(string message) => new ConsultarEnderecoPorCepFail(message);
    public static ConsultarEnderecoPorCepResult Unavailable(string message) => new ConsultarEnderecoPorCepUnavailable(message);
    public static ConsultarEnderecoPorCepResult Timeout(string message) => new ConsultarEnderecoPorCepTimeout(message);
}

public record ConsultarEnderecoPorCepOk(EnderecoConsulta Endereco) : ConsultarEnderecoPorCepResult;
public record ConsultarEnderecoPorCepBadRequest(string Message) : ConsultarEnderecoPorCepResult;
public record ConsultarEnderecoPorCepNotFound(string Message) : ConsultarEnderecoPorCepResult;
public record ConsultarEnderecoPorCepFail(string Message) : ConsultarEnderecoPorCepResult;
public record ConsultarEnderecoPorCepUnavailable(string Message) : ConsultarEnderecoPorCepResult;
public record ConsultarEnderecoPorCepTimeout(string Message) : ConsultarEnderecoPorCepResult;

public sealed class ConsultarEnderecoPorCepQueryValidator : AbstractValidator<ConsultarEnderecoPorCepQuery>
{
    public ConsultarEnderecoPorCepQueryValidator()
    {
        RuleFor(x => x.Cep)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe o CEP.")
            .Must(cep => CepBrasil.TentarLer(cep, out _, out _))
            .WithMessage("CEP deve ter exatamente 8 digitos ou o formato 00000-000.");
    }
}

public sealed class ConsultarEnderecoPorCepHandler : IRequestHandler<ConsultarEnderecoPorCepQuery, ConsultarEnderecoPorCepResult>
{
    private readonly IViaCepClient _client;
    private readonly ILogger<ConsultarEnderecoPorCepHandler> _logger;

    public ConsultarEnderecoPorCepHandler(IViaCepClient client, ILogger<ConsultarEnderecoPorCepHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ConsultarEnderecoPorCepResult> Handle(
        ConsultarEnderecoPorCepQuery request,
        CancellationToken cancellationToken)
    {
        if (!CepBrasil.TentarLer(request.Cep, out var digitos, out var formatado))
        {
            return ConsultarEnderecoPorCepResult.BadRequest("CEP deve ter exatamente 8 digitos ou o formato 00000-000.");
        }

        try
        {
            _logger.LogInformation("Consulta ViaCEP por CEP iniciada cep={Cep}", formatado);
            var endereco = await _client.ConsultarPorCepAsync(digitos, cancellationToken);
            if (endereco is null)
            {
                _logger.LogInformation("CEP valido nao encontrado no ViaCEP cep={Cep}", formatado);
                return ConsultarEnderecoPorCepResult.NotFound("CEP nao encontrado.");
            }

            _logger.LogInformation("Consulta ViaCEP por CEP concluida cep={Cep} uf={Uf} cidade={Cidade}", formatado, endereco.Uf, endereco.Cidade);
            return ConsultarEnderecoPorCepResult.Ok(endereco);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ViaCepException ex)
        {
            return MapearExcecao(ex, formatado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada na consulta ViaCEP cep={Cep}", formatado);
            return ConsultarEnderecoPorCepResult.Fail(ex.Message);
        }
    }

    private ConsultarEnderecoPorCepResult MapearExcecao(ViaCepException ex, string cep)
    {
        switch (ex.Kind)
        {
            case ViaCepErrorKind.InvalidRequest:
                _logger.LogWarning(ex, "Consulta ViaCEP rejeitada cep={Cep}", cep);
                return ConsultarEnderecoPorCepResult.BadRequest(ex.Message);
            case ViaCepErrorKind.NotFound:
                _logger.LogInformation(ex, "CEP nao encontrado no ViaCEP cep={Cep}", cep);
                return ConsultarEnderecoPorCepResult.NotFound(ex.Message);
            case ViaCepErrorKind.Unavailable:
                _logger.LogWarning(ex, "ViaCEP indisponivel cep={Cep}", cep);
                return ConsultarEnderecoPorCepResult.Unavailable(ex.Message);
            case ViaCepErrorKind.Timeout:
                _logger.LogWarning(ex, "Timeout na consulta ViaCEP cep={Cep}", cep);
                return ConsultarEnderecoPorCepResult.Timeout(ex.Message);
            default:
                _logger.LogError(ex, "Resposta invalida do ViaCEP cep={Cep}", cep);
                return ConsultarEnderecoPorCepResult.Fail(ex.Message);
        }
    }
}

public record PesquisarEnderecosQuery(string? Uf, string? Cidade, string? Logradouro) : IQuery<PesquisarEnderecosResult>;

public abstract record PesquisarEnderecosResult
{
    public static PesquisarEnderecosResult Ok(IReadOnlyList<EnderecoConsulta> enderecos) => new PesquisarEnderecosOk(enderecos);
    public static PesquisarEnderecosResult BadRequest(string message) => new PesquisarEnderecosBadRequest(message);
    public static PesquisarEnderecosResult Fail(string message) => new PesquisarEnderecosFail(message);
    public static PesquisarEnderecosResult Unavailable(string message) => new PesquisarEnderecosUnavailable(message);
    public static PesquisarEnderecosResult Timeout(string message) => new PesquisarEnderecosTimeout(message);
}

public record PesquisarEnderecosOk(IReadOnlyList<EnderecoConsulta> Enderecos) : PesquisarEnderecosResult;
public record PesquisarEnderecosBadRequest(string Message) : PesquisarEnderecosResult;
public record PesquisarEnderecosFail(string Message) : PesquisarEnderecosResult;
public record PesquisarEnderecosUnavailable(string Message) : PesquisarEnderecosResult;
public record PesquisarEnderecosTimeout(string Message) : PesquisarEnderecosResult;

public sealed class PesquisarEnderecosQueryValidator : AbstractValidator<PesquisarEnderecosQuery>
{
    public PesquisarEnderecosQueryValidator()
    {
        RuleFor(x => x.Uf)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a UF.")
            .Must(uf => CepBrasil.Ufs.Contains(uf!.Trim()))
            .WithMessage("UF invalida. Use uma das 27 siglas brasileiras, incluindo DF.");

        RuleFor(x => x.Cidade)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a cidade.")
            .Must(cidade => cidade!.Trim().Length >= 3)
            .WithMessage("Cidade deve ter pelo menos 3 caracteres.");

        RuleFor(x => x.Logradouro)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe o logradouro.")
            .Must(logradouro => logradouro!.Trim().Length >= 3)
            .WithMessage("Logradouro deve ter pelo menos 3 caracteres.");
    }
}

public sealed class PesquisarEnderecosHandler : IRequestHandler<PesquisarEnderecosQuery, PesquisarEnderecosResult>
{
    private readonly IViaCepClient _client;
    private readonly ILogger<PesquisarEnderecosHandler> _logger;

    public PesquisarEnderecosHandler(IViaCepClient client, ILogger<PesquisarEnderecosHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<PesquisarEnderecosResult> Handle(
        PesquisarEnderecosQuery request,
        CancellationToken cancellationToken)
    {
        var uf = request.Uf?.Trim().ToUpperInvariant() ?? string.Empty;
        var cidade = request.Cidade?.Trim() ?? string.Empty;
        var logradouro = request.Logradouro?.Trim() ?? string.Empty;

        if (!CepBrasil.Ufs.Contains(uf) || cidade.Length < 3 || logradouro.Length < 3)
        {
            return PesquisarEnderecosResult.BadRequest("Informe UF valida, cidade e logradouro com pelo menos 3 caracteres.");
        }

        try
        {
            _logger.LogInformation("Pesquisa ViaCEP iniciada uf={Uf} cidade={Cidade} logradouro={Logradouro}", uf, cidade, logradouro);
            var enderecos = await _client.PesquisarAsync(uf, cidade, logradouro, cancellationToken);
            _logger.LogInformation("Pesquisa ViaCEP concluida uf={Uf} resultados={Qtd}", uf, enderecos.Count);
            return PesquisarEnderecosResult.Ok(enderecos);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ViaCepException ex)
        {
            return MapearExcecao(ex, uf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada na pesquisa ViaCEP uf={Uf}", uf);
            return PesquisarEnderecosResult.Fail(ex.Message);
        }
    }

    private PesquisarEnderecosResult MapearExcecao(ViaCepException ex, string uf)
    {
        switch (ex.Kind)
        {
            case ViaCepErrorKind.InvalidRequest:
                _logger.LogWarning(ex, "Pesquisa ViaCEP rejeitada uf={Uf}", uf);
                return PesquisarEnderecosResult.BadRequest(ex.Message);
            case ViaCepErrorKind.Unavailable:
                _logger.LogWarning(ex, "ViaCEP indisponivel na pesquisa uf={Uf}", uf);
                return PesquisarEnderecosResult.Unavailable(ex.Message);
            case ViaCepErrorKind.Timeout:
                _logger.LogWarning(ex, "Timeout na pesquisa ViaCEP uf={Uf}", uf);
                return PesquisarEnderecosResult.Timeout(ex.Message);
            default:
                _logger.LogError(ex, "Resposta invalida do ViaCEP na pesquisa uf={Uf}", uf);
                return PesquisarEnderecosResult.Fail(ex.Message);
        }
    }
}
