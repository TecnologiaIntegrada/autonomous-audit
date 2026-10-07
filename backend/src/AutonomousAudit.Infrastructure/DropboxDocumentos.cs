using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class DropboxDocumentos : IDropboxDocumentos, IDisposable
{
    private readonly DropboxOptions _options;
    private readonly ILogger<DropboxDocumentos> _logger;
    private readonly object _gate = new();
    private DropboxClient? _client;
    private HttpClient? _httpClient;

    public DropboxDocumentos(IOptions<DropboxOptions> options, ILogger<DropboxDocumentos> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DropboxContaAtual> ObterContaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var conta = await GetClient().Users.GetCurrentAccountAsync();
            return new DropboxContaAtual(
                conta.Email,
                conta.Name.DisplayName,
                TipoConta(conta.AccountType),
                conta.Country,
                conta.Locale,
                conta.AccountId,
                conta.EmailVerified);
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<IReadOnlyList<DropboxEntrada>> ListarOuBuscarAsync(
        string? consulta,
        string? caminho,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pasta = NormalizarPasta(caminho);
            if (string.IsNullOrWhiteSpace(consulta))
            {
                return await ListarPastaAsync(pasta, cancellationToken);
            }

            return await BuscarAsync(consulta.Trim(), pasta, cancellationToken);
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<DropboxEntrada> EnviarAsync(
        string caminhoDestino,
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caminho = NormalizarArquivo(caminhoDestino);
            await GarantirPastaPaiAsync(caminho, cancellationToken);

            await using var buffer = new MemoryStream();
            await conteudo.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0)
            {
                throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "O arquivo nao pode estar vazio.");
            }

            buffer.Position = 0;
            var enviado = await GetClient().Files.UploadAsync(
                caminho,
                WriteMode.Overwrite.Instance,
                body: buffer);

            return Mapear(enviado);
        }
        catch (DropboxStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<DropboxEntrada> ExcluirAsync(string caminho, CancellationToken cancellationToken = default)
    {
        try
        {
            var resultado = await GetClient().Files.DeleteV2Async(NormalizarArquivo(caminho));
            return Mapear(resultado.Metadata);
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<DropboxEntrada> MoverAsync(
        string caminhoOrigem,
        string caminhoDestino,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var origem = NormalizarArquivo(caminhoOrigem);
            var destino = NormalizarArquivo(caminhoDestino);
            await GarantirPastaPaiAsync(destino, cancellationToken);
            var movido = await GetClient().Files.MoveV2Async(origem, destino);
            return Mapear(movido.Metadata);
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<string> ObterLinkTemporarioAsync(string caminho, CancellationToken cancellationToken = default)
    {
        try
        {
            var link = await GetClient().Files.GetTemporaryLinkAsync(NormalizarArquivo(caminho));
            if (string.IsNullOrWhiteSpace(link.Link))
            {
                throw new DropboxStorageException(DropboxErrorKind.Failed, "O armazenamento nao devolveu link temporario.");
            }

            return link.Link;
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public async Task<byte[]> BaixarAsync(string caminho, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizado = NormalizarArquivo(caminho);
            using var download = await GetClient().Files.DownloadAsync(normalizado);
            var bytes = await download.GetContentAsByteArrayAsync();
            if (bytes is null || bytes.Length == 0)
            {
                throw new DropboxStorageException(DropboxErrorKind.Failed, "O armazenamento devolveu arquivo vazio.");
            }

            return bytes;
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _httpClient?.Dispose();
    }

    private async Task<IReadOnlyList<DropboxEntrada>> ListarPastaAsync(string pasta, CancellationToken cancellationToken)
    {
        var itens = new List<DropboxEntrada>();
        var pagina = await GetClient().Files.ListFolderAsync(pasta);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            itens.AddRange(pagina.Entries.Select(Mapear));
            if (!pagina.HasMore || itens.Count >= 500)
            {
                break;
            }

            pagina = await GetClient().Files.ListFolderContinueAsync(pagina.Cursor);
        }

        return itens;
    }

    private async Task<IReadOnlyList<DropboxEntrada>> BuscarAsync(
        string consulta,
        string pasta,
        CancellationToken cancellationToken)
    {
        var opcoes = new SearchOptions(
            path: string.IsNullOrEmpty(pasta) ? null : pasta,
            maxResults: 100,
            filenameOnly: false);

        var itens = new List<DropboxEntrada>();
        var pagina = await GetClient().Files.SearchV2Async(consulta, opcoes);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var match in pagina.Matches)
            {
                if (match.Metadata.IsMetadata)
                {
                    itens.Add(Mapear(match.Metadata.AsMetadata.Value));
                }
            }

            if (!pagina.HasMore)
            {
                break;
            }

            pagina = await GetClient().Files.SearchContinueV2Async(pagina.Cursor);
        }

        return itens;
    }

    private async Task GarantirPastaPaiAsync(string caminho, CancellationToken cancellationToken)
    {
        var indice = caminho.LastIndexOf('/');
        if (indice <= 0)
        {
            return;
        }

        var pasta = caminho[..indice];
        try
        {
            await GetClient().Files.CreateFolderV2Async(pasta);
        }
        catch (ApiException<CreateFolderError>)
        {
            _logger.LogDebug("Pasta Dropbox ja existe ou nao precisa ser criada: {Pasta}", pasta);
        }
    }

    private DropboxClient GetClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        lock (_gate)
        {
            if (_client is not null)
            {
                return _client;
            }

            var temAccess = !string.IsNullOrWhiteSpace(_options.AccessToken);
            var temRefresh = !string.IsNullOrWhiteSpace(_options.RefreshToken);
            var temAppKey = !string.IsNullOrWhiteSpace(_options.AppKey);
            var temAppSecret = !string.IsNullOrWhiteSpace(_options.AppSecret);

            if (!temAccess && !temRefresh)
            {
                throw new DropboxStorageException(
                    DropboxErrorKind.Unauthorized,
                    "Armazenamento nao configurado.");
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds))
            };

            var config = new DropboxClientConfig("AutonomousAudit")
            {
                HttpClient = _httpClient
            };

            _client = temRefresh && temAppKey
                ? temAccess && temAppSecret
                    ? new DropboxClient(_options.AccessToken, _options.RefreshToken, _options.AppKey, _options.AppSecret, config)
                    : new DropboxClient(_options.RefreshToken, _options.AppKey, config)
                : new DropboxClient(_options.AccessToken, config);

            return _client;
        }
    }

    private static string TipoConta(Dropbox.Api.UsersCommon.AccountType? tipo)
    {
        if (tipo is null)
        {
            return "unknown";
        }

        if (tipo.IsPro)
        {
            return "pro";
        }

        if (tipo.IsBusiness)
        {
            return "business";
        }

        if (tipo.IsBasic)
        {
            return "basic";
        }

        return "other";
    }

    private static DropboxEntrada Mapear(Metadata metadata) =>
        metadata switch
        {
            FileMetadata arquivo => new DropboxEntrada(
                arquivo.Name,
                arquivo.PathDisplay ?? arquivo.PathLower ?? arquivo.Name,
                arquivo.Id,
                (long)arquivo.Size,
                new DateTimeOffset(DateTime.SpecifyKind(arquivo.ServerModified, DateTimeKind.Utc)),
                "arquivo"),
            FolderMetadata pasta => new DropboxEntrada(
                pasta.Name,
                pasta.PathDisplay ?? pasta.PathLower ?? pasta.Name,
                pasta.Id,
                null,
                null,
                "pasta"),
            DeletedMetadata excluido => new DropboxEntrada(
                excluido.Name,
                excluido.PathDisplay ?? excluido.PathLower ?? excluido.Name,
                null,
                null,
                null,
                "excluido"),
            _ => new DropboxEntrada(
                metadata.Name,
                metadata.PathDisplay ?? metadata.PathLower ?? metadata.Name,
                null,
                null,
                null,
                "outro")
        };

    private static string NormalizarPasta(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || caminho.Trim() == "/")
        {
            return string.Empty;
        }

        var valor = caminho.Replace('\\', '/').Trim();
        return valor.StartsWith('/') ? valor : "/" + valor;
    }

    private static string NormalizarArquivo(string caminho)
    {
        var valor = NormalizarPasta(caminho);
        if (string.IsNullOrEmpty(valor))
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Informe um caminho de arquivo valido.");
        }

        return valor;
    }

    private static Exception Map(Exception ex)
    {
        if (ex is DropboxStorageException)
        {
            return ex;
        }

        if (ex is AuthException auth)
        {
            if (auth.ErrorResponse.IsMissingScope)
            {
                var escopo = auth.ErrorResponse.AsMissingScope.Value.RequiredScope;
                return new DropboxStorageException(
                    DropboxErrorKind.Forbidden,
                    $"O aplicativo de armazenamento nao tem o escopo '{escopo}'.",
                    ex);
            }

            return new DropboxStorageException(
                DropboxErrorKind.Unauthorized,
                "Token de armazenamento expirado ou invalido.",
                ex);
        }

        if (ex.Message.Contains("required scope", StringComparison.OrdinalIgnoreCase))
        {
            return new DropboxStorageException(
                DropboxErrorKind.Forbidden,
                MensagemEscopo(ex.Message),
                ex);
        }

        if (ex is ApiException<DeleteError> delete && delete.ErrorResponse.IsPathLookup)
        {
            return new DropboxStorageException(DropboxErrorKind.NotFound, "Arquivo nao encontrado no armazenamento.", ex);
        }

        if (ex is ApiException<DownloadError>)
        {
            return new DropboxStorageException(DropboxErrorKind.NotFound, "Arquivo nao encontrado no armazenamento.", ex);
        }

        if (ex is ApiException<ListFolderError> list && list.ErrorResponse.IsPath)
        {
            return new DropboxStorageException(DropboxErrorKind.NotFound, "Pasta nao encontrada no armazenamento.", ex);
        }

        if (ex is ApiException<SearchError>)
        {
            return new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Consulta de armazenamento invalida.", ex);
        }

        if (ex is RateLimitException)
        {
            return new DropboxStorageException(DropboxErrorKind.Failed, "Limite de requisicoes do armazenamento atingido. Tente novamente em instantes.", ex);
        }

        return new DropboxStorageException(
                DropboxErrorKind.Failed,
                "Falha no armazenamento.",
                ex,
                responseBody: LogExcecao.Truncar(ex.ToString()));
    }

    private static string MensagemEscopo(string detalhe)
    {
        const string padrao = "O aplicativo de armazenamento nao tem a permissao necessaria.";
        var inicio = detalhe.IndexOf("required scope '", StringComparison.OrdinalIgnoreCase);
        if (inicio < 0)
        {
            return padrao;
        }

        var escopoInicio = inicio + "required scope '".Length;
        var escopoFim = detalhe.IndexOf('\'', escopoInicio);
        if (escopoFim <= escopoInicio)
        {
            return padrao;
        }

        var escopo = detalhe[escopoInicio..escopoFim];
        return $"O aplicativo de armazenamento nao tem o escopo '{escopo}'.";
    }
}
