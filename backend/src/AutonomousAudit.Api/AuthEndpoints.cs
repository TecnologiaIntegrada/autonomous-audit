using System.Net;
using System.Text.Json.Serialization;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousAudit.Api;

public static class AuthEndpoints
{
    public static WebApplication MapAuth(this WebApplication app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Autenticacao");

        group.MapPost("/login", Login)
            .Produces<LoginResponse>()
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Autentica o usuário e emite o JWT de acesso")
            .WithDescription(SwaggerDocs.Bloco(
                "Porta de entrada da API. Confere e-mail e senha e, quando o cadastro não exige MFA, devolve o JWT usado em todas as outras rotas.",
                "Sempre que um operador, sistema ou integração precisar de um token novo. O JWT anterior vale 1 hora e não é renovado automaticamente.",
                "- Rota pública (não envie Bearer neste passo).\n" +
                "- Headers obrigatórios: `email` e `senha` (não vão no JSON).\n" +
                "- Se o usuário tiver MFA: após o primeiro 200 com `desafio`, reenvie os mesmos headers mais `sms_auth_code` e/ou `email_auth_code`.",
                "1. Valida o e-mail e a senha (hash no banco; a senha em texto nunca é gravada).\n" +
                "2. Se `sms_auth` ou `email_auth` estiverem ligados no cadastro, dispara o código de 6 dígitos e **não** emite o JWT ainda.\n" +
                "3. Com os códigos corretos (ou sem MFA), emite JWT HS256 com o id do usuário e o `jti` da sessão.",
                "- **200** com `{ \"token\": \"...\" }` — autenticado.\n" +
                "- **200** com `{ \"desafio\", \"mensagem\" }` — falta o código MFA.\n" +
                "- **401** e-mail ou senha inválidos, ou código MFA incorreto.\n" +
                "- **502** falha ao enviar SMS/e-mail de desafio.",
                atencao: "O token não tem refresh. Guarde-o só em memória/cliente e autentique de novo ao receber 401."));

        group.MapGet("/me", Me)
            .RequireRecurso(RecursoChaves.UsuarioRead)
            .Produces<UsuarioResponse>();

        group.MapPut("/me", AtualizarMe)
            .RequireRecurso(RecursoChaves.UsuarioUpdate)
            .Accepts<AtualizarMeuPerfilBody>("application/json")
            .Produces<UsuarioResponse>();

        group.MapPost("/me/verificar", SolicitarVerificacao)
            .RequireRecurso(RecursoChaves.UsuarioUpdate)
            .Accepts<VerificarContatoBody>("application/json")
            .Produces<MensagemSimplesResponse>();

        group.MapPost("/me/verificar/confirmar", ConfirmarVerificacao)
            .RequireRecurso(RecursoChaves.UsuarioUpdate)
            .Accepts<ConfirmarContatoBody>("application/json")
            .Produces<UsuarioResponse>();

        group.MapPost("/me/api-token", EmitirTokenRelatorio)
            .RequireRecurso(RecursoChaves.RelatorioRead)
            .Produces<TokenRelatorioResponse>();

        group.MapPost("/google", Google)
            .Accepts<GoogleLoginBody>("application/json")
            .Produces<GoogleAuthResponse>()
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Conflict)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Autentica com o id_token do Google")
            .WithDescription(SwaggerDocs.Bloco(
                "Valida o id_token na Google. Se o `sub` já existe em usuarios, emite o JWT de acesso. Se não existe, devolve um token curto de cadastro.",
                "Depois do OAuth no frontend, antes do dashboard.",
                "- Rota pública.\n" +
                "- JSON `{ \"idToken\": \"...\" }`.\n" +
                "- O e-mail Google precisa estar verificado.",
                "1. Valida o JWT da Google (audience = ClientId).\n" +
                "2. Busca `google_sub`.\n" +
                "3. Se achar, emite JWT HS256 de 1 hora.\n" +
                "4. Se o e-mail já existir sem esse sub, responde 409.\n" +
                "5. Caso contrário, emite `registroToken` (20 min) para POST /v1/auth/google/cadastro.",
                "- **200** `{ status: \"pronto\", token }`.\n" +
                "- **200** `{ status: \"cadastro_pendente\", registroToken, email, nome, foto }`.\n" +
                "- **401** token Google inválido.\n" +
                "- **409** e-mail já cadastrado com senha."));

        group.MapPost("/google/cadastro", GoogleCadastro)
            .Accepts<GoogleCadastroBody>("application/json")
            .Produces<LoginResponse>()
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Conclui o cadastro de um usuário Google novo")
            .WithDescription(SwaggerDocs.Bloco(
                "Cria o usuário com `google_sub`, senha, telefone e e-mail confirmado (igual ao da conta Google).",
                "Tela de novo usuário após o Google, quando o sub ainda não está no banco.",
                "- Rota pública, mas exige `registroToken` do POST /google.\n" +
                "- JSON: nome, senha, senhaConfirmacao, emailConfirmacao, telefonePrincipal, cep, endereco, numero, bairro, cidade, uf, pais.",
                "Confere o token de cadastro, o e-mail digitado, a senha e o endereço. Grava o usuário com google_sub e foto, aplica o catálogo de recursos e devolve o JWT pleno.",
                "- **200** `{ token }`.\n" +
                "- **400** senha/e-mail/telefone inválidos.\n" +
                "- **401** registroToken expirado."));

        group.MapPost("/recuperar-senha", RecuperarSenha)
            .Accepts<RecuperarSenhaBody>("application/json")
            .Produces<RecuperarSenhaResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Envia um código de recuperação de senha por e-mail")
            .WithDescription(SwaggerDocs.Bloco(
                "Se o e-mail existir, enfileira um código de 6 dígitos (15 minutos) pela mesma fila SMTP do MFA.",
                "Fluxo 'esqueci minha senha' no portal.",
                "- Rota pública.\n" +
                "- JSON `{ \"email\": \"...\" }`.",
                "Não revela se o e-mail existe: a resposta 200 é a mesma. O código vai por e-mail.",
                "- **200** `{ \"mensagem\": \"...\" }`.\n" +
                "- **400** e-mail inválido.\n" +
                "- **502** falha ao enfileirar o e-mail."));

        group.MapPost("/redefinir-senha", RedefinirSenha)
            .Accepts<RedefinirSenhaBody>("application/json")
            .Produces<LoginResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Troca a senha com o código recebido por e-mail")
            .WithDescription(SwaggerDocs.Bloco(
                "Confere e-mail + código e grava a nova senha (hash). Devolve JWT para seguir autenticado.",
                "Tela de redefinição após o e-mail de recuperação.",
                "- Rota pública.\n" +
                "- JSON: email, codigo (6 dígitos), senha, senhaConfirmacao.",
                "Código inválido ou vencido vira 400. Senha mínima de 8 caracteres.",
                "- **200** `{ token }`.\n" +
                "- **400** código/senha inválidos."));

        group.MapPost("/cadastro", Cadastro)
            .Accepts<CadastroPublicoBody>("application/json")
            .Produces<LoginResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Cadastra um usuário sem Google")
            .WithDescription(SwaggerDocs.Bloco(
                "Cria o cadastro com nome, e-mail, telefone, senha e endereço. Aplica o catálogo de recursos e devolve o JWT.",
                "Tela 'Criar uma conta' para quem não usa Google.",
                "- Rota pública.\n" +
                "- JSON: nome, email, emailConfirmacao, telefonePrincipal, senha, senhaConfirmacao, cep, endereco, numero, bairro, cidade, uf, pais.",
                "Valida e-mail único, hasheia a senha, atribui os mesmos recursos do cadastro Google e emite o JWT de 1 hora.",
                "- **200** `{ token }`.\n" +
                "- **400** e-mail em uso ou dados inválidos."));

        group.MapGet("/cadastro/cep/{cep}", CepCadastroPublico)
            .RequireRateLimiting("viacep")
            .Produces<EnderecoResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.TooManyRequests)
            .WithSummary("Consulta CEP durante o cadastro sem Google")
            .WithDescription(SwaggerDocs.Bloco(
                "Mesma busca ViaCEP do cadastro Google, sem exigir registroToken.",
                "Tela de novo usuário por e-mail, para preencher endereço. O front não chama o ViaCEP.",
                "- Rota pública, com limite de consultas.",
                "Consulta o CEP no backend.",
                "- **200** endereço.\n- **404** CEP inexistente."));

        group.MapGet("/google/cep/{cep}", CepCadastro)
            .RequireRateLimiting("viacep")
            .Produces<EnderecoResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.TooManyRequests)
            .WithSummary("Consulta CEP durante o cadastro Google")
            .WithDescription(SwaggerDocs.Bloco(
                "Mesma busca ViaCEP do GET /v1/enderecos/cep, liberada só com o registroToken do cadastro Google.",
                "Tela de novo usuário, para preencher logradouro, bairro, cidade e UF sem criar usuário stub.",
                "- Rota pública, mas exige header `X-Registro-Token`.\n" +
                "- O front não chama o ViaCEP.",
                "Valida o token de cadastro (20 min) e consulta o CEP no backend.",
                "- **200** endereço.\n" +
                "- **401** registroToken ausente ou expirado.\n" +
                "- **404** CEP inexistente."));

        return app;
    }

    private static async Task<IResult> Login(
        [FromHeader(Name = "email")] string? email,
        [FromHeader(Name = "senha")] string? senha,
        [FromHeader(Name = "sms_auth_code")] string? smsAuthCode,
        [FromHeader(Name = "email_auth_code")] string? emailAuthCode,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AutenticarUsuarioCommand(
                email ?? string.Empty,
                senha ?? string.Empty,
                smsAuthCode,
                emailAuthCode),
            cancellationToken);

        return result switch
        {
            AutenticarUsuarioOk ok => Results.Ok(LoginResponse.ComToken(ok.Jwt.Token)),
            AutenticarUsuarioDesafio desafio => Results.Ok(LoginResponse.ComDesafio(desafio.Desafio, desafio.Mensagem)),
            AutenticarUsuarioUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            AutenticarUsuarioFail failed => Problem("Falha na autenticacao", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Google(
        [FromBody] GoogleLoginBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AutenticarGoogleCommand(body.IdToken ?? string.Empty), cancellationToken);
        return result switch
        {
            AutenticarGooglePronto ok => Results.Ok(GoogleAuthResponse.Pronto(ok.Jwt.Token)),
            AutenticarGoogleCadastroPendente pendente => Results.Ok(
                GoogleAuthResponse.CadastroPendente(
                    pendente.Registro.Token, pendente.Email, pendente.Nome, pendente.Foto)),
            AutenticarGoogleEmailEmUso conflito => Problem("Conflito", conflito.Message, StatusCodes.Status409Conflict),
            AutenticarGoogleUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            AutenticarGoogleFail failed => Problem("Falha na autenticacao Google", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> GoogleCadastro(
        [FromBody] GoogleCadastroBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CompletarCadastroGoogleCommand(
                body.RegistroToken ?? string.Empty,
                body.Nome ?? string.Empty,
                body.Senha ?? string.Empty,
                body.SenhaConfirmacao ?? string.Empty,
                body.EmailConfirmacao ?? string.Empty,
                body.TelefonePrincipal ?? string.Empty,
                body.Cep ?? string.Empty,
                body.Endereco ?? string.Empty,
                body.Numero ?? string.Empty,
                body.Bairro ?? string.Empty,
                body.Cidade ?? string.Empty,
                body.Uf ?? string.Empty,
                body.Pais ?? string.Empty),
            cancellationToken);
        return result switch
        {
            CompletarCadastroGoogleOk ok => Results.Ok(LoginResponse.ComToken(ok.Jwt.Token)),
            CompletarCadastroGoogleBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            CompletarCadastroGoogleUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            CompletarCadastroGoogleFail failed => Problem("Falha no cadastro Google", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> RecuperarSenha(
        [FromBody] RecuperarSenhaBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SolicitarRecuperacaoSenhaCommand(body.Email ?? string.Empty),
            cancellationToken);
        return result switch
        {
            SolicitarRecuperacaoSenhaOk => Results.Ok(new RecuperarSenhaResponse(
                "Se o e-mail existir, enviamos um codigo de recuperacao.")),
            SolicitarRecuperacaoSenhaFail failed => Problem("Falha ao recuperar senha", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> RedefinirSenha(
        [FromBody] RedefinirSenhaBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RedefinirSenhaCommand(
                body.Email ?? string.Empty,
                body.Codigo ?? string.Empty,
                body.Senha ?? string.Empty,
                body.SenhaConfirmacao ?? string.Empty),
            cancellationToken);
        return result switch
        {
            RedefinirSenhaOk ok => Results.Ok(LoginResponse.ComToken(ok.Jwt.Token)),
            RedefinirSenhaBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            RedefinirSenhaFail failed => Problem("Falha ao redefinir senha", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Me(HttpContext http, IMediator mediator, CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid id)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(new ObterUsuarioQuery(id), cancellationToken);
        return result switch
        {
            ObterUsuarioOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            ObterUsuarioNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ObterUsuarioFail failed => Problem("Falha ao obter usuario", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> AtualizarMe(
        HttpContext http,
        [FromBody] AtualizarMeuPerfilBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid id)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(
            new AtualizarMeuPerfilCommand(
                id,
                body.Nome ?? string.Empty,
                body.EmailSecundario,
                body.TelefonePrincipal,
                body.TelefoneSecundario,
                body.Cidade,
                body.Endereco,
                body.Cep,
                body.Uf,
                body.Pais),
            cancellationToken);
        return result switch
        {
            AtualizarUsuarioOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            AtualizarUsuarioNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AtualizarUsuarioBadRequest bad => Problem("Requisicao invalida", bad.Message, StatusCodes.Status400BadRequest),
            AtualizarUsuarioFail failed => Problem("Falha ao atualizar perfil", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> SolicitarVerificacao(
        HttpContext http,
        [FromBody] VerificarContatoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid id)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(
            new SolicitarVerificacaoContatoCommand(id, body.Tipo ?? string.Empty, body.Canal ?? string.Empty),
            cancellationToken);
        return result switch
        {
            SolicitarVerificacaoContatoOk ok => Results.Ok(new MensagemSimplesResponse(ok.Mensagem)),
            SolicitarVerificacaoContatoBadRequest bad => Problem("Requisicao invalida", bad.Message, StatusCodes.Status400BadRequest),
            SolicitarVerificacaoContatoFail failed => Problem("Falha ao enviar codigo", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ConfirmarVerificacao(
        HttpContext http,
        [FromBody] ConfirmarContatoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid id)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(
            new ConfirmarVerificacaoContatoCommand(
                id,
                body.Tipo ?? string.Empty,
                body.Canal ?? string.Empty,
                body.Codigo ?? string.Empty),
            cancellationToken);
        return result switch
        {
            ConfirmarVerificacaoContatoOk ok => Results.Ok(UsuarioResponse.From(ok.Usuario, incluirPermissoes: true)),
            ConfirmarVerificacaoContatoBadRequest bad => Problem("Requisicao invalida", bad.Message, StatusCodes.Status400BadRequest),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> EmitirTokenRelatorio(
        HttpContext http,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid id)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(new EmitirTokenRelatorioCommand(id), cancellationToken);
        return result switch
        {
            EmitirTokenRelatorioOk ok => Results.Ok(new TokenRelatorioResponse(ok.Token, ok.ExpiraEm)),
            _ => Problem("Nao encontrado", "Usuario nao encontrado.", StatusCodes.Status404NotFound)
        };
    }

    private static async Task<IResult> Cadastro(
        [FromBody] CadastroPublicoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CadastrarUsuarioPublicoCommand(
                body.Nome ?? string.Empty,
                body.Email ?? string.Empty,
                body.EmailConfirmacao ?? string.Empty,
                body.TelefonePrincipal ?? string.Empty,
                body.Senha ?? string.Empty,
                body.SenhaConfirmacao ?? string.Empty,
                body.Cep ?? string.Empty,
                body.Endereco ?? string.Empty,
                body.Numero ?? string.Empty,
                body.Bairro ?? string.Empty,
                body.Cidade ?? string.Empty,
                body.Uf ?? string.Empty,
                body.Pais ?? string.Empty),
            cancellationToken);
        return result switch
        {
            CadastrarUsuarioPublicoOk ok => Results.Ok(LoginResponse.ComToken(ok.Jwt.Token)),
            CadastrarUsuarioPublicoBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            CadastrarUsuarioPublicoFail failed => Problem("Falha no cadastro", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> CepCadastroPublico(
        string cep,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarEnderecoPorCepQuery(cep), cancellationToken);
        return MapCepCadastro(result);
    }

    private static async Task<IResult> CepCadastro(
        string cep,
        [FromHeader(Name = "X-Registro-Token")] string? registroToken,
        IJwtTokenService jwt,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (jwt.LerCadastroGoogle(registroToken ?? string.Empty) is null)
        {
            return Problem("Nao autorizado", "Sessao de cadastro expirada. Entre de novo com o Google.", StatusCodes.Status401Unauthorized);
        }

        var result = await mediator.Send(new ConsultarEnderecoPorCepQuery(cep), cancellationToken);
        return MapCepCadastro(result);
    }

    private static IResult MapCepCadastro(ConsultarEnderecoPorCepResult result) =>
        result switch
        {
            ConsultarEnderecoPorCepOk ok => Results.Ok(EnderecoResponse.From(ok.Endereco)),
            ConsultarEnderecoPorCepBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            ConsultarEnderecoPorCepNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ConsultarEnderecoPorCepFail failed => Problem("Falha no ViaCEP", failed.Message, StatusCodes.Status502BadGateway),
            ConsultarEnderecoPorCepUnavailable unavailable => Problem("ViaCEP indisponivel", unavailable.Message, StatusCodes.Status503ServiceUnavailable),
            ConsultarEnderecoPorCepTimeout timeout => Problem("Tempo esgotado", timeout.Message, StatusCodes.Status504GatewayTimeout),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

    private static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}

public sealed record LoginResponse(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Token,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Desafio,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Mensagem)
{
    public static LoginResponse ComToken(string token) => new(token, null, null);

    public static LoginResponse ComDesafio(string desafio, string mensagem) =>
        new(null, desafio, mensagem);
}

public sealed record GoogleLoginBody(string? IdToken);

public sealed record GoogleCadastroBody(
    string? RegistroToken,
    string? Nome,
    string? Senha,
    string? SenhaConfirmacao,
    string? EmailConfirmacao,
    string? TelefonePrincipal,
    string? Cep,
    string? Endereco,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Pais);

public sealed record CadastroPublicoBody(
    string? Nome,
    string? Email,
    string? EmailConfirmacao,
    string? TelefonePrincipal,
    string? Senha,
    string? SenhaConfirmacao,
    string? Cep,
    string? Endereco,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Pais);

public sealed record AtualizarMeuPerfilBody(
    string? Nome,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string? Cidade,
    string? Endereco,
    string? Cep,
    string? Uf,
    string? Pais);

public sealed record VerificarContatoBody(string? Tipo, string? Canal);

public sealed record ConfirmarContatoBody(string? Tipo, string? Canal, string? Codigo);

public sealed record MensagemSimplesResponse(string Mensagem);

public sealed record TokenRelatorioResponse(string Token, DateTimeOffset ExpiraEm);

public sealed record RecuperarSenhaBody(string? Email);

public sealed record RedefinirSenhaBody(
    string? Email,
    string? Codigo,
    string? Senha,
    string? SenhaConfirmacao);

public sealed record RecuperarSenhaResponse(string Mensagem);

public sealed record GoogleAuthResponse(
    string Status,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Token,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? RegistroToken,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Email,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Nome,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Foto)
{
    public static GoogleAuthResponse Pronto(string token) =>
        new("pronto", token, null, null, null, null);

    public static GoogleAuthResponse CadastroPendente(string registroToken, string email, string nome, string? foto) =>
        new("cadastro_pendente", null, registroToken, email, nome, foto);
}

public sealed record UsuarioResponse(
    Guid Id,
    string Nome,
    string EmailPrincipal,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string TipoDocto,
    string NDocto,
    DateOnly? DataEmissao,
    string Orgao,
    string Cidade,
    string Endereco,
    string Cep,
    string Uf,
    string Pais,
    bool SmsAuth,
    bool EmailAuth,
    bool EmailPrincipalVerificado,
    bool EmailSecundarioVerificado,
    bool TelefonePrincipalVerificado,
    bool TelefoneSecundarioVerificado,
    DateTimeOffset? ApiTokenExpira,
    long QwenTokensTotal,
    decimal QwenCustoTotalBrl,
    long PerplexityTokensTotal,
    decimal PerplexityCustoTotalBrl,
    string Token,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    string? GooglePicture,
    IReadOnlyList<PermissaoResponse>? Permissoes)
{
    [JsonPropertyName("geminiTokensTotal")]
    public long GeminiTokensTotal => PerplexityTokensTotal > 0 ? PerplexityTokensTotal : QwenTokensTotal;

    [JsonPropertyName("geminiCustoTotalBrl")]
    public decimal GeminiCustoTotalBrl => PerplexityCustoTotalBrl > 0 ? PerplexityCustoTotalBrl : QwenCustoTotalBrl;

    public static UsuarioResponse From(Usuario usuario, bool incluirPermissoes = false) =>
        new(
            usuario.Id,
            usuario.Nome,
            usuario.EmailPrincipal,
            usuario.EmailSecundario,
            usuario.TelefonePrincipal,
            usuario.TelefoneSecundario,
            usuario.TipoDocto,
            usuario.NDocto,
            usuario.DataEmissao,
            usuario.Orgao,
            usuario.Cidade,
            usuario.Endereco,
            usuario.Cep,
            usuario.Uf,
            usuario.Pais,
            usuario.SmsAuth,
            usuario.EmailAuth,
            usuario.EmailPrincipalVerificado,
            usuario.EmailSecundarioVerificado,
            usuario.TelefonePrincipalVerificado,
            usuario.TelefoneSecundarioVerificado,
            usuario.ApiTokenExpira,
            0,
            0,
            usuario.PerplexityTokensTotal,
            usuario.PerplexityCustoTotalBrl,
            usuario.Token,
            usuario.DataCriacao,
            usuario.DataAtualizacao,
            usuario.GooglePicture,
            incluirPermissoes
                ? usuario.Permissoes.Select(PermissaoResponse.From).ToList()
                : null);
}

public sealed record PermissaoResponse(Guid Id, Guid ModuloId, Guid RecursoId, string? Chave, string? Nome)
{
    public static PermissaoResponse From(UsuarioPermissao permissao) =>
        new(
            permissao.Id,
            permissao.ModuloId,
            permissao.RecursoId,
            permissao.Recurso?.Chave,
            permissao.Recurso?.Nome);
}

public static class EndpointValidation
{
    public static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);

    public static IResult Validation(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
}
