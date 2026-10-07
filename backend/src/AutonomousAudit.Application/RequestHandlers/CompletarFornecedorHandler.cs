using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record CompletarFornecedorCommand(Guid UsuarioId, string Nome) : IQuery<CompletarFornecedorResult>;

public abstract record CompletarFornecedorResult
{
    public static CompletarFornecedorResult Ok(
        string? razaoSocial,
        string? cpfCnpj,
        string? telefone,
        string? endereco,
        IReadOnlyList<string> citacoes,
        IReadOnlyList<string> campos) =>
        new CompletarFornecedorOk(razaoSocial, cpfCnpj, telefone, endereco, citacoes, campos);

    public static CompletarFornecedorResult BadRequest(string message) => new CompletarFornecedorBadRequest(message);
    public static CompletarFornecedorResult Unauthorized(string message) => new CompletarFornecedorUnauthorized(message);
    public static CompletarFornecedorResult NotFound(string message) => new CompletarFornecedorNotFound(message);
    public static CompletarFornecedorResult Fail(string message) => new CompletarFornecedorFail(message);
}

public record CompletarFornecedorOk(
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco,
    IReadOnlyList<string> Citacoes,
    IReadOnlyList<string> CamposPreenchidos) : CompletarFornecedorResult;

public record CompletarFornecedorBadRequest(string Message) : CompletarFornecedorResult;
public record CompletarFornecedorUnauthorized(string Message) : CompletarFornecedorResult;
public record CompletarFornecedorNotFound(string Message) : CompletarFornecedorResult;
public record CompletarFornecedorFail(string Message) : CompletarFornecedorResult;

public sealed class CompletarFornecedorCommandValidator : AbstractValidator<CompletarFornecedorCommand>
{
    public CompletarFornecedorCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public sealed class CompletarFornecedorHandler : IRequestHandler<CompletarFornecedorCommand, CompletarFornecedorResult>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPerplexityClient _perplexity;
    private readonly ILogger<CompletarFornecedorHandler> _logger;

    public CompletarFornecedorHandler(IPerplexityClient perplexity, ILogger<CompletarFornecedorHandler> logger)
    {
        _perplexity = perplexity;
        _logger = logger;
    }

    public async Task<CompletarFornecedorResult> Handle(
        CompletarFornecedorCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Pesquisa de fornecedor na internet iniciada UsuarioId={UsuarioId} Nome={Nome}",
            request.UsuarioId,
            request.Nome);

        try
        {
            var resposta = await _perplexity.CompletarAsync(
                [
                    new PerplexityMensagem("system", ContextoSistema()),
                    new PerplexityMensagem("user", MontarPergunta(request.Nome))
                ],
                cancellationToken);

            if (!TentarLerSugestao(resposta.Texto, out var sugestao) || sugestao is null)
            {
                _logger.LogWarning(
                    "Pesquisa de fornecedor na internet sem JSON valido UsuarioId={UsuarioId} Nome={Nome} caracteres={Caracteres}",
                    request.UsuarioId,
                    request.Nome,
                    resposta.Texto?.Length ?? 0);
                return CompletarFornecedorResult.NotFound("A pesquisa nao encontrou informacoes novas para este cadastro.");
            }

            var razaoSocial = Aplicar(sugestao.RazaoSocial);
            var cpfCnpj = AplicarCpfCnpj(sugestao.CpfCnpj);
            var telefone = Aplicar(sugestao.Telefone);
            var endereco = Aplicar(sugestao.Endereco);
            var preenchidos = new List<string>();
            if (razaoSocial is not null) preenchidos.Add("razaoSocial");
            if (cpfCnpj is not null) preenchidos.Add("cpfCnpj");
            if (telefone is not null) preenchidos.Add("telefone");
            if (endereco is not null) preenchidos.Add("endereco");

            if (preenchidos.Count == 0)
            {
                return CompletarFornecedorResult.NotFound("A pesquisa nao encontrou informacoes novas para este cadastro.");
            }

            _logger.LogInformation(
                "Pesquisa de fornecedor na internet concluida UsuarioId={UsuarioId} Nome={Nome} CamposPreenchidos={CamposPreenchidos} Citacoes={Citacoes}",
                request.UsuarioId,
                request.Nome,
                string.Join(",", preenchidos),
                resposta.Citacoes.Count);

            return CompletarFornecedorResult.Ok(
                razaoSocial,
                cpfCnpj,
                telefone,
                endereco,
                resposta.Citacoes,
                preenchidos);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Pesquisa de fornecedor na internet sem autorizacao Perplexity");
            return CompletarFornecedorResult.Unauthorized(ex.Message);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.InvalidRequest)
        {
            _logger.LogWarning(ex, "Pesquisa de fornecedor na internet rejeitada pelo Perplexity");
            return CompletarFornecedorResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha na pesquisa de fornecedor na internet UsuarioId={UsuarioId} Nome={Nome} tipo={Tipo} cadeia={Cadeia}",
                request.UsuarioId,
                request.Nome,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            return CompletarFornecedorResult.Fail(ex.Message);
        }
    }

    private static string ContextoSistema() =>
        "Voce pesquisa dados cadastrais publicos de empresas e pessoas juridicas no Brasil pelo nome. " +
        "Responda somente com um JSON valido, sem markdown e sem texto fora do objeto. " +
        "Nao invente CNPJ, telefone ou endereco. Se nao encontrar um campo com confianca, omita-o ou use null.";

    private static string MontarPergunta(string nome) =>
        "Pesquise dados cadastrais publicos desta empresa ou prestador no Brasil usando somente o nome.\n\n" +
        $"Nome: {nome.Trim()}\n\n" +
        "Devolva exatamente este JSON:\n" +
        "{\"razaoSocial\":\"...\",\"cpfCnpj\":\"...\",\"telefone\":\"...\",\"endereco\":\"...\"}\n\n" +
        "Regras:\n" +
        "- Use apenas o nome informado para localizar a empresa.\n" +
        "- cpfCnpj so com digitos (11 CPF ou 14 CNPJ).\n" +
        "- telefone no formato brasileiro, com DDD.\n" +
        "- endereco completo: logradouro, numero, bairro, CEP, cidade e UF.\n" +
        "- Prefira fontes publicas (Receita, site da empresa, cadastros comerciais).";

    private static bool TentarLerSugestao(string? texto, out FornecedorSugestaoDto? sugestao)
    {
        sugestao = null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var bruto = texto.Trim();
        var inicio = bruto.IndexOf('{');
        var fim = bruto.LastIndexOf('}');
        if (inicio < 0 || fim <= inicio)
        {
            return false;
        }

        try
        {
            sugestao = JsonSerializer.Deserialize<FornecedorSugestaoDto>(bruto[inicio..(fim + 1)], Json);
            return sugestao is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Aplicar(string? sugerido)
    {
        if (string.IsNullOrWhiteSpace(sugerido))
        {
            return null;
        }

        var valor = sugerido.Trim();
        return valor.Length == 0 ? null : valor;
    }

    private static string? AplicarCpfCnpj(string? sugerido)
    {
        var digitos = TextoNormalizado.Digitos(sugerido);
        return digitos is { Length: 11 or 14 } ? digitos : null;
    }

    private sealed class FornecedorSugestaoDto
    {
        public string? RazaoSocial { get; init; }
        public string? CpfCnpj { get; init; }
        public string? Telefone { get; init; }
        public string? Endereco { get; init; }
    }
}
