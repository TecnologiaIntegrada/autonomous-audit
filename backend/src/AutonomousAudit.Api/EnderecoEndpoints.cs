using System.Net;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousAudit.Api;

public static class EnderecoEndpoints
{
    public static WebApplication MapEnderecos(this WebApplication app)
    {
        var group = app.MapGroup("/v1/enderecos")
            .WithTags("Enderecos")
            .RequireRateLimiting("viacep");

        group.MapGet("/cep/{cep}", ConsultarPorCep)
            .RequireRecurso(RecursoChaves.EnderecoRead)
            .Produces<EnderecoResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.TooManyRequests)
            .Produces((int)HttpStatusCode.BadGateway)
            .Produces((int)HttpStatusCode.ServiceUnavailable)
            .Produces((int)HttpStatusCode.GatewayTimeout)
            .WithSummary("Consulta um endereço brasileiro pelo CEP")
            .WithDescription(SwaggerDocs.Bloco(
                "Busca o endereço postal no ViaCEP a partir de um CEP. A consulta é feita no backend, via HTTPS.",
                "Preenchimento de cadastro (usuário, cliente) quando a pessoa informa o CEP e a tela precisa sugerir logradouro, bairro, cidade e UF.",
                "- JWT + recurso `endereco.read`.\n" +
                "- `{cep}` com 8 dígitos (`01310100`) ou `00000-000`. Zeros à esquerda são preservados.\n" +
                "- Não envie URL do ViaCEP: o destino é só a base configurada na API.",
                "Valida o formato **antes** de chamar o provedor. Remove o hífen só depois da validação. CEP inexistente no ViaCEP (HTTP 200 com `erro: true`) vira 404 aqui. Bairro ou logradouro vazios **não** invalidam o endereço. O complemento postal do provedor não substitui número do imóvel informado pelo usuário.",
                "- **200** um objeto de endereço (`pais=BR`, `origem=ViaCEP`, CEP em `00000-000`).\n" +
                "- **400** CEP com letras, hífen fora do lugar ou quantidade errada de dígitos.\n" +
                "- **401/403** JWT ausente ou sem o recurso.\n" +
                "- **404** CEP válido, mas não encontrado.\n" +
                "- **429** limite desta API (não é paginação do ViaCEP).\n" +
                "- **502** JSON inválido ou falha inesperada do provedor.\n" +
                "- **503** ViaCEP indisponível, falha de conexão ou limite do provedor.\n" +
                "- **504** tempo limite da consulta.",
                atencao: "O ViaCEP devolve no máximo o que o provedor conhecer para aquele CEP. Não há número de imóvel nem coordenadas."));

        group.MapGet("/pesquisa", Pesquisar)
            .RequireRecurso(RecursoChaves.EnderecoRead)
            .Produces<IReadOnlyList<EnderecoResponse>>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.TooManyRequests)
            .Produces((int)HttpStatusCode.BadGateway)
            .Produces((int)HttpStatusCode.ServiceUnavailable)
            .Produces((int)HttpStatusCode.GatewayTimeout)
            .WithSummary("Pesquisa endereços por UF, cidade e logradouro")
            .WithDescription(SwaggerDocs.Bloco(
                "Lista endereços correspondentes no ViaCEP. A pesquisa ocorre no backend, via HTTPS.",
                "Autocomplete de logradouro quando o usuário já escolheu UF e cidade e digitou parte da rua.",
                "- JWT + recurso `endereco.read`.\n" +
                "- Query obrigatória: `uf`, `cidade`, `logradouro`.\n" +
                "- UF: uma das 27 siglas (incluindo DF). Cidade e logradouro: no mínimo 3 caracteres, com acentos preservados.",
                "Normaliza a UF para maiúsculas, aplica Trim em cidade/logradouro e monta a URL só com a base ViaCEP configurada. O provedor devolve **no máximo 50** resultados; a API não pagina e **não** afirma que a lista contém todos os endereços da cidade.",
                "- **200** lista (pode ser `[]` se não houver correspondência).\n" +
                "- **400** UF, cidade ou logradouro inválidos.\n" +
                "- **401/403** JWT ausente ou sem o recurso.\n" +
                "- **429** limite desta API.\n" +
                "- **502/503/504** falha, indisponibilidade ou timeout do ViaCEP.",
                atencao: "Máximo de 50 itens do ViaCEP. Sem resultados = lista vazia, não 404."));

        return app;
    }

    private static async Task<IResult> ConsultarPorCep(
        string cep,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarEnderecoPorCepQuery(cep), cancellationToken);
        return result switch
        {
            ConsultarEnderecoPorCepOk ok => Results.Ok(EnderecoResponse.From(ok.Endereco)),
            ConsultarEnderecoPorCepBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            ConsultarEnderecoPorCepNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ConsultarEnderecoPorCepFail failed => EndpointValidation.Problem("Falha no ViaCEP", failed.Message, StatusCodes.Status502BadGateway),
            ConsultarEnderecoPorCepUnavailable unavailable => EndpointValidation.Problem("ViaCEP indisponivel", unavailable.Message, StatusCodes.Status503ServiceUnavailable),
            ConsultarEnderecoPorCepTimeout timeout => EndpointValidation.Problem("Tempo esgotado", timeout.Message, StatusCodes.Status504GatewayTimeout),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Pesquisar(
        [FromQuery] string? uf,
        [FromQuery] string? cidade,
        [FromQuery] string? logradouro,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PesquisarEnderecosQuery(uf, cidade, logradouro), cancellationToken);
        return result switch
        {
            PesquisarEnderecosOk ok => Results.Ok(ok.Enderecos.Select(EnderecoResponse.From).ToList()),
            PesquisarEnderecosBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            PesquisarEnderecosFail failed => EndpointValidation.Problem("Falha no ViaCEP", failed.Message, StatusCodes.Status502BadGateway),
            PesquisarEnderecosUnavailable unavailable => EndpointValidation.Problem("ViaCEP indisponivel", unavailable.Message, StatusCodes.Status503ServiceUnavailable),
            PesquisarEnderecosTimeout timeout => EndpointValidation.Problem("Tempo esgotado", timeout.Message, StatusCodes.Status504GatewayTimeout),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}

public sealed record EnderecoResponse(
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
    string Origem)
{
    public static EnderecoResponse From(EnderecoConsulta endereco) =>
        new(
            endereco.Cep,
            endereco.Logradouro,
            endereco.Complemento,
            endereco.Unidade,
            endereco.Bairro,
            endereco.Cidade,
            endereco.Uf,
            endereco.Estado,
            endereco.Regiao,
            endereco.CodigoIbge,
            endereco.CodigoGia,
            endereco.Ddd,
            endereco.CodigoSiafi,
            endereco.Pais,
            endereco.Origem);
}
