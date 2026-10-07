using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Dapper;

namespace AutonomousAudit.Infrastructure;

internal sealed class UsuarioRow
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string EmailPrincipal { get; set; } = string.Empty;
    public string? EmailSecundario { get; set; }
    public string? TelefonePrincipal { get; set; }
    public string? TelefoneSecundario { get; set; }
    public string TipoDocto { get; set; } = string.Empty;
    public string NDocto { get; set; } = string.Empty;
    public DateTime? DataEmissao { get; set; }
    public string Orgao { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public bool SmsAuth { get; set; }
    public bool EmailAuth { get; set; }
    public bool EmailPrincipalVerificado { get; set; }
    public bool EmailSecundarioVerificado { get; set; }
    public bool TelefonePrincipalVerificado { get; set; }
    public bool TelefoneSecundarioVerificado { get; set; }
    public string? ApiTokenJti { get; set; }
    public DateTimeOffset? ApiTokenExpira { get; set; }
    public string Token { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string? GoogleSub { get; set; }
    public string? GooglePicture { get; set; }
    public long PerplexityTokensTotal { get; set; }
    public decimal PerplexityCustoTotalBrl { get; set; }
    public DateTimeOffset DataCriacao { get; set; }
    public DateTimeOffset DataAtualizacao { get; set; }

    public Usuario ParaDominio(IEnumerable<UsuarioPermissao>? permissoes = null) =>
        Usuario.Hidratar(
            Id,
            Nome,
            EmailPrincipal,
            EmailSecundario,
            TelefonePrincipal,
            TelefoneSecundario,
            TipoDocto,
            NDocto,
            DataEmissao is null ? null : DateOnly.FromDateTime(DateTime.SpecifyKind(DataEmissao.Value, DateTimeKind.Unspecified)),
            Orgao,
            Cidade,
            Endereco,
            Cep,
            Uf,
            Pais,
            SmsAuth,
            EmailAuth,
            Token,
            SenhaHash,
            DataCriacao,
            DataAtualizacao,
            permissoes,
            GoogleSub,
            GooglePicture,
            emailPrincipalVerificado: EmailPrincipalVerificado,
            emailSecundarioVerificado: EmailSecundarioVerificado,
            telefonePrincipalVerificado: TelefonePrincipalVerificado,
            telefoneSecundarioVerificado: TelefoneSecundarioVerificado,
            apiTokenJti: ApiTokenJti,
            apiTokenExpira: ApiTokenExpira,
            perplexityTokensTotal: PerplexityTokensTotal,
            perplexityCustoTotalBrl: PerplexityCustoTotalBrl);
}

internal sealed class RecursoRow
{
    public Guid Id { get; set; }
    public Guid ModuloId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Chave { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string MetodoHttp { get; set; } = string.Empty;
    public string Rota { get; set; } = string.Empty;

    public Recurso ParaDominio() =>
        Recurso.Hidratar(Id, ModuloId, Codigo, Chave, Nome, MetodoHttp, Rota);
}

internal sealed class ModuloRow
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;

    public Modulo ParaDominio() => new(Id, Codigo, Nome, Descricao);
}

internal sealed class PermissaoRow
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid ModuloId { get; set; }
    public Guid RecursoId { get; set; }
}

internal sealed class DropboxDispatchRow
{
    public Guid Id { get; set; }
    public Guid ArquivoRecebidoId { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string NomeOriginal { get; set; } = string.Empty;
    public string CaminhoLocal { get; set; } = string.Empty;
    public string CaminhoDropbox { get; set; } = string.Empty;
    public string? DropboxId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Tentativas { get; set; }
    public int MaxTentativas { get; set; }
    public string? UltimaMensagemErro { get; set; }
    public DateTimeOffset DataCriacao { get; set; }
    public DateTimeOffset DataAtualizacao { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }

    public DropboxDispatch ParaDominio() =>
        DropboxDispatch.Hidratar(
            Id,
            ArquivoRecebidoId,
            NomeArquivo,
            NomeOriginal,
            CaminhoLocal,
            CaminhoDropbox,
            DropboxId,
            Enum.TryParse<DropboxDispatchStatus>(Status, ignoreCase: true, out var status)
                ? status
                : DropboxDispatchStatus.Pendente,
            Tentativas,
            MaxTentativas,
            UltimaMensagemErro,
            DataCriacao,
            DataAtualizacao,
            DataConclusao);
}

internal sealed class ContaEmailRow
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Remetente { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public int? PopPort { get; set; }
    public string Protocol { get; set; } = "SMTP";
    public string TlsMode { get; set; } = "starttls";

    public ContaEmail ParaDominio() =>
        ContaEmail.Hidratar(Id, Email, Password, Server, SmtpPort, PopPort, Protocol, TlsMode, Remetente);
}

public sealed class UsuarioConsulta : IUsuarioConsulta
{
    private const string SqlUsuario = """
        SELECT id AS Id,
               nome AS Nome,
               email_principal AS EmailPrincipal,
               email_secundario AS EmailSecundario,
               telefone_principal AS TelefonePrincipal,
               telefone_secundario AS TelefoneSecundario,
               tipo_docto AS TipoDocto,
               n_docto AS NDocto,
               data_emissao AS DataEmissao,
               orgao AS Orgao,
               cidade AS Cidade,
               endereco AS Endereco,
               cep AS Cep,
               uf AS Uf,
               pais AS Pais,
               sms_auth AS SmsAuth,
               email_auth AS EmailAuth,
               email_principal_verificado AS EmailPrincipalVerificado,
               email_secundario_verificado AS EmailSecundarioVerificado,
               telefone_principal_verificado AS TelefonePrincipalVerificado,
               telefone_secundario_verificado AS TelefoneSecundarioVerificado,
               api_token_jti AS ApiTokenJti,
               api_token_expira AS ApiTokenExpira,
               token AS Token,
               senha_hash AS SenhaHash,
               google_sub AS GoogleSub,
               google_picture AS GooglePicture,
               perplexity_tokens_total AS PerplexityTokensTotal,
               perplexity_custo_total_brl AS PerplexityCustoTotalBrl,
               data_criacao AS DataCriacao,
               data_atualizacao AS DataAtualizacao
        FROM usuarios
        """;

    private const string SqlPermissoes = """
        SELECT p.id AS Id,
               p.usuario_id AS UsuarioId,
               p.modulo_id AS ModuloId,
               p.recurso_id AS RecursoId,
               r.id AS Id,
               r.modulo_id AS ModuloId,
               r.codigo AS Codigo,
               r.chave AS Chave,
               r.nome AS Nome,
               r.metodo_http AS MetodoHttp,
               r.rota AS Rota
        FROM usuario_permissoes p
        LEFT JOIN recursos r ON r.id = p.recurso_id
        WHERE p.usuario_id = @Id
        """;

    private readonly ISqlConnectionFactory _connections;

    public UsuarioConsulta(ISqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<Usuario?> ObterPorIdAsync(Guid id, bool incluirPermissoes, CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioRow>(
            new CommandDefinition(
                SqlUsuario + " WHERE id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
        if (usuario is null)
        {
            return null;
        }

        if (!incluirPermissoes)
        {
            return usuario.ParaDominio();
        }

        var permissoes = await ListarPermissoesAsync(connection, id, cancellationToken);
        return usuario.ParaDominio(permissoes);
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var linhas = await connection.QueryAsync<UsuarioRow>(
            new CommandDefinition(
                SqlUsuario + " ORDER BY nome",
                cancellationToken: cancellationToken));
        return linhas.Select(l => l.ParaDominio()).ToList();
    }

    private static async Task<IReadOnlyList<UsuarioPermissao>> ListarPermissoesAsync(
        System.Data.IDbConnection connection,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var permissoes = await connection.QueryAsync<PermissaoRow, RecursoRow, UsuarioPermissao>(
            new CommandDefinition(SqlPermissoes, new { Id = usuarioId }, cancellationToken: cancellationToken),
            (permissao, recurso) => UsuarioPermissao.Hidratar(
                permissao.Id,
                permissao.UsuarioId,
                permissao.ModuloId,
                permissao.RecursoId,
                recurso is null || recurso.Id == Guid.Empty ? null : recurso.ParaDominio()),
            splitOn: "Id");
        return permissoes.ToList();
    }
}

public sealed class CatalogoConsulta : ICatalogoConsulta
{
    private const string SqlModulos = """
        SELECT id AS Id, codigo AS Codigo, nome AS Nome, descricao AS Descricao
        FROM modulos
        ORDER BY nome
        """;

    private const string SqlRecursos = """
        SELECT id AS Id,
               modulo_id AS ModuloId,
               codigo AS Codigo,
               chave AS Chave,
               nome AS Nome,
               metodo_http AS MetodoHttp,
               rota AS Rota
        FROM recursos
        """;

    private readonly ISqlConnectionFactory _connections;

    public CatalogoConsulta(ISqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<Modulo>> ListarModulosComRecursosAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var modulos = (await connection.QueryAsync<ModuloRow>(
            new CommandDefinition(SqlModulos, cancellationToken: cancellationToken))).ToList();
        var recursos = (await connection.QueryAsync<RecursoRow>(
            new CommandDefinition(SqlRecursos + " ORDER BY chave", cancellationToken: cancellationToken))).ToList();
        var porModulo = recursos.GroupBy(r => r.ModuloId).ToDictionary(g => g.Key, g => g.ToList());
        return modulos.Select(modulo =>
        {
            var dominio = modulo.ParaDominio();
            if (porModulo.TryGetValue(modulo.Id, out var lista))
            {
                dominio.HidratarRecursos(lista.Select(r => r.ParaDominio()));
            }

            return dominio;
        }).ToList();
    }

    public async Task<Modulo?> ObterModuloPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var modulo = await connection.QuerySingleOrDefaultAsync<ModuloRow>(
            new CommandDefinition(
                "SELECT id AS Id, codigo AS Codigo, nome AS Nome, descricao AS Descricao FROM modulos WHERE id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
        if (modulo is null)
        {
            return null;
        }

        var recursos = await connection.QueryAsync<RecursoRow>(
            new CommandDefinition(
                SqlRecursos + " WHERE modulo_id = @Id ORDER BY chave",
                new { Id = id },
                cancellationToken: cancellationToken));
        var dominio = modulo.ParaDominio();
        dominio.HidratarRecursos(recursos.Select(r => r.ParaDominio()));
        return dominio;
    }

    public async Task<IReadOnlyList<Recurso>> ListarRecursosAsync(Guid? moduloId, CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var sql = SqlRecursos + (moduloId.HasValue ? " WHERE modulo_id = @ModuloId" : string.Empty) + " ORDER BY chave";
        var linhas = await connection.QueryAsync<RecursoRow>(
            new CommandDefinition(sql, new { ModuloId = moduloId }, cancellationToken: cancellationToken));
        return linhas.Select(r => r.ParaDominio()).ToList();
    }

    public async Task<Recurso?> ObterRecursoPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var linha = await connection.QuerySingleOrDefaultAsync<RecursoRow>(
            new CommandDefinition(
                SqlRecursos + " WHERE id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
        return linha?.ParaDominio();
    }
}

public sealed class DropboxDispatchConsulta : IDropboxDispatchConsulta
{
    private readonly ISqlConnectionFactory _connections;

    public DropboxDispatchConsulta(ISqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<DropboxDispatch?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var linha = await connection.QuerySingleOrDefaultAsync<DropboxDispatchRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       arquivo_recebido_id AS ArquivoRecebidoId,
                       nome_arquivo AS NomeArquivo,
                       nome_original AS NomeOriginal,
                       caminho_local AS CaminhoLocal,
                       caminho_dropbox AS CaminhoDropbox,
                       dropbox_id AS DropboxId,
                       status AS Status,
                       tentativas AS Tentativas,
                       max_tentativas AS MaxTentativas,
                       ultima_mensagem_erro AS UltimaMensagemErro,
                       data_criacao AS DataCriacao,
                       data_atualizacao AS DataAtualizacao,
                       data_conclusao AS DataConclusao
                FROM dropbox_dispatch
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
        return linha?.ParaDominio();
    }
}

public sealed class ContaEmailConsulta : IContaEmailConsulta
{
    private readonly ISqlConnectionFactory _connections;

    public ContaEmailConsulta(ISqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ContaEmail>> ListarAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connections.Create();
        var linhas = await connection.QueryAsync<ContaEmailRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       email AS Email,
                       remetente AS Remetente,
                       password AS Password,
                       server AS Server,
                       smtp_port AS SmtpPort,
                       pop_port AS PopPort,
                       protocol AS Protocol,
                       tls_mode AS TlsMode
                FROM mail_accounts
                ORDER BY email
                """,
                cancellationToken: cancellationToken));
        return linhas.Select(l => l.ParaDominio()).ToList();
    }
}
