using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class UsuarioEndpoints
{
    public static WebApplication MapUsuarios(this WebApplication app)
    {
        var group = app.MapGroup("/v1/usuarios").WithTags("Usuarios");

        group.MapGet("", Listar)
            .RequireRecurso(RecursoChaves.UsuarioRead)
            .RestrictUsuarioAoProprioOuAdmin()
            .Produces<IReadOnlyList<UsuarioResponse>>()
            .WithSummary("Lista os usuários do sistema")
            .WithDescription(SwaggerDocs.Bloco(
                "Relação das pessoas cadastradas para acessar a API. Não inclui senha.",
                "Administração de acesso: ver quem existe antes de conceder permissões ou desativar alguém.",
                "- JWT + recurso `usuario.read`.\n" +
                "- Chamador precisa estar em `adm_sys` (listar outros usuários nunca é autoatendimento).",
                "Devolve nome, e-mails, telefones, documento, flags de MFA e o identificador persistente `token` (não é o JWT). A lista não traz o detalhe de permissões; use GET por id ou GET .../permissoes.",
                "- **200** lista de usuários.\n" +
                "- **403** sem vinculo em ADM_SYS.",
                atencao: "Permissao de catalogo nao basta: so administradores do sistema listam cadastros."));

        group.MapGet("/{id:guid}", Obter)
            .RequireRecurso(RecursoChaves.UsuarioRead)
            .RestrictUsuarioAoProprioOuAdmin()
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Obtém o cadastro completo de um usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Ficha de um usuário, incluindo as permissões atuais.",
                "Tela de edição, conferência de MFA e do que a pessoa pode fazer na API.",
                "- JWT + recurso `usuario.read`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.",
                "Lê o cadastro e a lista de recursos atribuídos. Senha nunca retorna.",
                "- **200** usuário + permissões.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **404** id inexistente."));

        group.MapPost("", Criar)
            .RequireRecurso(RecursoChaves.UsuarioCreate)
            .RestrictUsuarioAoProprioOuAdmin()
            .Accepts<CriarUsuarioBody>("application/json")
            .Produces<UsuarioResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Cadastra um novo usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Cria a pessoa que poderá autenticar. A senha entra em texto neste JSON e é gravada só como hash.",
                "Onboarding de operador, auditor ou integração humana.",
                "- JWT + recurso `usuario.create` + vinculo em ADM_SYS.\n" +
                "- JSON: dados cadastrais, `senha`, `smsAuth`/`emailAuth` (MFA) e `recursos` opcional (chaves como `email.send`).",
                "Valida e-mail único, hasheia a senha e, se `recursos` vier preenchido, já associa as permissões.",
                "- **201** usuário criado (sem senha).\n" +
                "- **400** e-mail duplicado ou dados inválidos.\n" +
                "- **403** chamador fora de ADM_SYS."));

        group.MapPut("/{id:guid}", Atualizar)
            .RequireRecurso(RecursoChaves.UsuarioUpdate)
            .RestrictUsuarioAoProprioOuAdmin()
            .Accepts<AtualizarUsuarioBody>("application/json")
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Atualiza os dados de um usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Altera cadastro (incluindo ligar/desligar MFA). A senha só muda se o campo `senha` vier preenchido.",
                "Correção de telefone, documento, e-mail ou troca de senha a pedido da pessoa.",
                "- JWT + recurso `usuario.update`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.\n" +
                "- JSON com os mesmos campos do cadastro; `senha` opcional.",
                "Atualiza a ficha. Permissões **não** são alteradas aqui — use as rotas `/permissoes`.",
                "- **200** cadastro atualizado.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **404** usuário inexistente.\n" +
                "- **400** e-mail em uso por outro usuário."));

        group.MapDelete("/{id:guid}", Excluir)
            .RequireRecurso(RecursoChaves.UsuarioDelete)
            .RestrictUsuarioAoProprioOuAdmin()
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Exclui um usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Remove o cadastro e as permissões daquela pessoa. O JWT dela deixa de ser válido na prática (usuário some do banco).",
                "Offboarding. Ação irreversível.",
                "- JWT + recurso `usuario.delete`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.",
                "Apaga o usuário e os vínculos em `usuario_permissoes`. Nao permite excluir o ultimo registro de ADM_SYS.",
                "- **204** excluído.\n" +
                "- **400** tentativa de excluir o ultimo administrador.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **404** já inexistente."));

        group.MapGet("/{id:guid}/permissoes", ListarPermissoes)
            .RequireRecurso(RecursoChaves.PermissoesRead)
            .RestrictUsuarioAoProprioOuAdmin()
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Lista as permissões de um usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Mostra quais recursos (ações da API) aquele usuário pode executar.",
                "Auditoria de acesso ou tela de perfil de autorização.",
                "- JWT + recurso `permissoes.read`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.",
                "Devolve o usuário com a coleção `permissoes` (chave tipo `email.send`, módulo e recurso).",
                "- **200** usuário + permissões.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **404** usuário inexistente."));

        group.MapPut("/{id:guid}/permissoes", SubstituirPermissoes)
            .RequireRecurso(RecursoChaves.PermissoesAssign)
            .RestrictUsuarioAoProprioOuAdmin()
            .Accepts<SubstituirPermissoesBody>("application/json")
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Substitui todas as permissões do usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Define o conjunto completo de acessos: o que não estiver na lista é revogado.",
                "Quando o papel da pessoa muda (ex.: passou a ser só leitura) e o perfil precisa ser reescrito.",
                "- JWT + recurso `permissoes.assign`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.\n" +
                "- JSON `{ \"recursos\": [\"email.send\", \"arquivo.read\", ...] }`. Lista vazia remove tudo.",
                "Apaga as permissões atuais e grava só as chaves informadas (precisam existir no catálogo).",
                "- **200** usuário com o novo conjunto.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **400** chave inexistente no catálogo."));

        group.MapPost("/{id:guid}/permissoes", AdicionarPermissao)
            .RequireRecurso(RecursoChaves.PermissoesAssign)
            .RestrictUsuarioAoProprioOuAdmin()
            .Accepts<AdicionarPermissaoBody>("application/json")
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Concede mais um recurso ao usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Inclui uma permissão sem mexer nas demais.",
                "Liberar um módulo pontual (ex.: passou a enviar e-mail).",
                "- JWT + recurso `permissoes.assign`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.\n" +
                "- JSON `{ \"recurso\": \"email.send\" }`.",
                "Associa a chave ao usuário se ela existir no catálogo e ainda não estiver atribuída.",
                "- **200** usuário atualizado.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **400** recurso inválido ou já atribuído."));

        group.MapDelete("/{id:guid}/permissoes/{recursoId:guid}", RemoverPermissao)
            .RequireRecurso(RecursoChaves.PermissoesAssign)
            .RestrictUsuarioAoProprioOuAdmin()
            .Produces<UsuarioResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Revoga um recurso do usuário")
            .WithDescription(SwaggerDocs.Bloco(
                "Tira uma permissão específica, mantendo as outras.",
                "Restringir acesso sem reescrever o perfil inteiro.",
                "- JWT + recurso `permissoes.assign`.\n" +
                "- `{id}` do proprio usuario, ou qualquer id se o chamador estiver em ADM_SYS.\n" +
                "- `{recursoId}` é o GUID do recurso (não a chave texto). Obtenha-o em GET /v1/recursos ou na lista de permissões.",
                "Remove o vínculo em `usuario_permissoes`.",
                "- **200** usuário atualizado.\n" +
                "- **403** `{id}` de outro usuario sem ADM_SYS.\n" +
                "- **404** usuário ou permissão inexistente."));

        return app;
    }

    private static async Task<IResult> Listar(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarUsuariosQuery(), cancellationToken);
        return result switch
        {
            ListarUsuariosOk ok => Results.Ok(ok.Usuarios.Select(u => UsuarioResponse.From(u)).ToList()),
            ListarUsuariosFail failed => EndpointValidation.Problem("Falha ao listar usuarios", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Obter(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ObterUsuarioQuery(id), cancellationToken);
        return result switch
        {
            ObterUsuarioOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            ObterUsuarioNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ObterUsuarioFail failed => EndpointValidation.Problem("Falha ao obter usuario", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Criar(
        [FromBody] CriarUsuarioBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CriarUsuarioCommand(body.ParaDados(), body.Senha ?? string.Empty, body.Recursos),
            cancellationToken);

        return result switch
        {
            CriarUsuarioCreated created => Results.Created(
                $"/v1/usuarios/{created.Usuario.Id}",
                UsuarioResponse.From(created.Usuario, incluirPermissoes: true)),
            CriarUsuarioBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            CriarUsuarioFail failed => EndpointValidation.Problem("Falha ao criar usuario", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Atualizar(
        Guid id,
        [FromBody] AtualizarUsuarioBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AtualizarUsuarioCommand(id, body.ParaDados(), body.Senha),
            cancellationToken);

        return result switch
        {
            AtualizarUsuarioOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario)),
            AtualizarUsuarioNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AtualizarUsuarioBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AtualizarUsuarioFail failed => EndpointValidation.Problem("Falha ao atualizar usuario", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Excluir(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExcluirUsuarioCommand(id), cancellationToken);
        return result switch
        {
            ExcluirUsuarioOk => Results.NoContent(),
            ExcluirUsuarioNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ExcluirUsuarioBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            ExcluirUsuarioFail failed => EndpointValidation.Problem("Falha ao excluir usuario", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ListarPermissoes(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarPermissoesUsuarioQuery(id), cancellationToken);
        return result switch
        {
            ListarPermissoesUsuarioOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            ListarPermissoesUsuarioNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ListarPermissoesUsuarioFail failed => EndpointValidation.Problem("Falha ao listar permissoes", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> SubstituirPermissoes(
        Guid id,
        [FromBody] SubstituirPermissoesBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SubstituirPermissoesCommand(id, body.Recursos ?? []),
            cancellationToken);
        return MapPermissao(result);
    }

    private static async Task<IResult> AdicionarPermissao(
        Guid id,
        [FromBody] AdicionarPermissaoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AdicionarPermissaoCommand(id, body.Recurso ?? string.Empty),
            cancellationToken);
        return MapPermissao(result);
    }

    private static async Task<IResult> RemoverPermissao(
        Guid id,
        Guid recursoId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoverPermissaoCommand(id, recursoId), cancellationToken);
        return MapPermissao(result);
    }

    private static IResult MapPermissao(AlterarPermissoesResult result) =>
        result switch
        {
            AlterarPermissoesOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            AlterarPermissoesNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AlterarPermissoesBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AlterarPermissoesFail failed => EndpointValidation.Problem("Falha ao alterar permissoes", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
}

public sealed record CriarUsuarioBody(
    string? Nome,
    string? EmailPrincipal,
    string? Senha,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string? TipoDocto,
    string? NDocto,
    DateOnly? DataEmissao,
    string? Orgao,
    string? Cidade,
    string? Endereco,
    string? Cep,
    string? Uf,
    string? Pais,
    bool SmsAuth = false,
    bool EmailAuth = false,
    IReadOnlyList<string>? Recursos = null)
{
    public DadosUsuario ParaDados() =>
        new(
            Nome ?? string.Empty,
            EmailPrincipal ?? string.Empty,
            EmailSecundario,
            TelefonePrincipal,
            TelefoneSecundario,
            TipoDocto,
            NDocto,
            DataEmissao,
            Orgao,
            Cidade,
            Endereco,
            Cep,
            Uf,
            Pais,
            SmsAuth,
            EmailAuth);
}

public sealed record AtualizarUsuarioBody(
    string? Nome,
    string? EmailPrincipal,
    string? Senha,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string? TipoDocto,
    string? NDocto,
    DateOnly? DataEmissao,
    string? Orgao,
    string? Cidade,
    string? Endereco,
    string? Cep,
    string? Uf,
    string? Pais,
    bool SmsAuth = false,
    bool EmailAuth = false)
{
    public DadosUsuario ParaDados() =>
        new(
            Nome ?? string.Empty,
            EmailPrincipal ?? string.Empty,
            EmailSecundario,
            TelefonePrincipal,
            TelefoneSecundario,
            TipoDocto,
            NDocto,
            DataEmissao,
            Orgao,
            Cidade,
            Endereco,
            Cep,
            Uf,
            Pais,
            SmsAuth,
            EmailAuth);
}

public sealed record SubstituirPermissoesBody(IReadOnlyList<string>? Recursos);

public sealed record AdicionarPermissaoBody(string? Recurso);

public sealed record ModuloResponse(
    Guid Id,
    string Codigo,
    string Nome,
    string Descricao,
    IReadOnlyList<RecursoCatalogoResponse> Recursos)
{
    public static ModuloResponse From(Modulo modulo) =>
        new(
            modulo.Id,
            modulo.Codigo,
            modulo.Nome,
            modulo.Descricao,
            modulo.Recursos
                .OrderBy(r => r.Chave)
                .Select(RecursoCatalogoResponse.From)
                .ToList());
}

public sealed record RecursoCatalogoResponse(
    Guid Id,
    string Codigo,
    string Chave,
    string Nome,
    string MetodoHttp,
    string Rota)
{
    public static RecursoCatalogoResponse From(Recurso recurso) =>
        new(recurso.Id, recurso.Codigo, recurso.Chave, recurso.Nome, recurso.MetodoHttp, recurso.Rota);
}
