using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record EnviarDocumentoDropboxCommand(string? Caminho, string NomeArquivo, Stream Conteudo, Guid UsuarioId)
    : ICommand<EnviarDocumentoDropboxResult>;

public abstract record EnviarDocumentoDropboxResult
{
    public static EnviarDocumentoDropboxResult Created(DropboxEntrada arquivo) =>
        new EnviarDocumentoDropboxCreated(arquivo);

    public static EnviarDocumentoDropboxResult BadRequest(string message) =>
        new EnviarDocumentoDropboxBadRequest(message);

    public static EnviarDocumentoDropboxResult Unauthorized(string message) =>
        new EnviarDocumentoDropboxUnauthorized(message);

    public static EnviarDocumentoDropboxResult Forbidden(string message) =>
        new EnviarDocumentoDropboxForbidden(message);

    public static EnviarDocumentoDropboxResult Fail(string message) =>
        new EnviarDocumentoDropboxFail(message);
}

public record EnviarDocumentoDropboxCreated(DropboxEntrada Arquivo) : EnviarDocumentoDropboxResult;
public record EnviarDocumentoDropboxBadRequest(string Message) : EnviarDocumentoDropboxResult;
public record EnviarDocumentoDropboxUnauthorized(string Message) : EnviarDocumentoDropboxResult;
public record EnviarDocumentoDropboxForbidden(string Message) : EnviarDocumentoDropboxResult;
public record EnviarDocumentoDropboxFail(string Message) : EnviarDocumentoDropboxResult;

public sealed class EnviarDocumentoDropboxCommandValidator : AbstractValidator<EnviarDocumentoDropboxCommand>
{
    public EnviarDocumentoDropboxCommandValidator()
    {
        RuleFor(x => x.NomeArquivo)
            .NotEmpty().WithMessage("O arquivo e obrigatorio.");

        RuleFor(x => x.Conteudo)
            .NotNull().WithMessage("O conteudo do arquivo e obrigatorio.");

        RuleFor(x => x.Caminho)
            .MaximumLength(1000).WithMessage("O caminho deve ter no maximo 1000 caracteres.");

        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}

public sealed class EnviarDocumentoDropboxHandler
    : IRequestHandler<EnviarDocumentoDropboxCommand, EnviarDocumentoDropboxResult>
{
    private readonly IDropboxDocumentos _dropbox;
    private readonly ILogger<EnviarDocumentoDropboxHandler> _logger;

    public EnviarDocumentoDropboxHandler(IDropboxDocumentos dropbox, ILogger<EnviarDocumentoDropboxHandler> logger)
    {
        _dropbox = dropbox;
        _logger = logger;
    }

    public async Task<EnviarDocumentoDropboxResult> Handle(
        EnviarDocumentoDropboxCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Conteudo.CanSeek && request.Conteudo.Length == 0)
            {
                _logger.LogWarning("Upload Dropbox rejeitado: arquivo vazio {NomeArquivo}", request.NomeArquivo);
                return EnviarDocumentoDropboxResult.BadRequest("O arquivo nao pode estar vazio.");
            }

            var destino = DropboxPathHelper.Destino(request.Caminho, request.NomeArquivo, request.UsuarioId);
            _logger.LogInformation("Enviando arquivo para Dropbox {Destino}", destino);
            var enviado = await _dropbox.EnviarAsync(destino, request.Conteudo, cancellationToken);
            _logger.LogInformation("Arquivo enviado ao Dropbox {Caminho} {Id}", enviado.Caminho, enviado.Id);
            return EnviarDocumentoDropboxResult.Created(enviado);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Dropbox nao autorizado no envio {NomeArquivo}", request.NomeArquivo);
            return EnviarDocumentoDropboxResult.Unauthorized(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Forbidden)
        {
            _logger.LogWarning(ex, "Dropbox sem permissao no envio {NomeArquivo}", request.NomeArquivo);
            return EnviarDocumentoDropboxResult.Forbidden(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.InvalidRequest)
        {
            _logger.LogWarning(ex, "Upload Dropbox invalido {NomeArquivo}", request.NomeArquivo);
            return EnviarDocumentoDropboxResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar arquivo ao Dropbox {NomeArquivo}", request.NomeArquivo);
            return EnviarDocumentoDropboxResult.Fail(ex.Message);
        }
    }
}

public static class DropboxPathHelper
{
    public const string PastaPadrao = "/AutonomousAudit";

    public static string PastaDoUsuario(Guid usuarioId) =>
        PastaPadrao + "/" + usuarioId.ToString("D");

    public static string DestinoNaRaiz(string nomeArquivo)
    {
        var arquivo = NomeParaDropbox(nomeArquivo);
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Nome de arquivo invalido.");
        }

        return PastaPadrao + "/" + arquivo;
    }

    public static string Destino(string? caminho, string nomeArquivo, Guid usuarioId)
    {
        var arquivo = NomeParaDropbox(nomeArquivo);
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Nome de arquivo invalido.");
        }

        if (usuarioId == Guid.Empty)
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Usuario autenticado obrigatorio para o destino do armazenamento.");
        }

        var pastaUsuario = PastaDoUsuario(usuarioId);
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return pastaUsuario + "/" + arquivo;
        }

        var normalizado = Normalizar(caminho);
        if (normalizado.StartsWith(PastaPadrao + "/", StringComparison.OrdinalIgnoreCase))
        {
            normalizado = normalizado[PastaPadrao.Length..];
        }

        normalizado = normalizado.Trim('/');
        if (normalizado.StartsWith(usuarioId.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            var resto = normalizado[usuarioId.ToString("D").Length..].Trim('/');
            normalizado = resto;
        }

        if (string.IsNullOrWhiteSpace(normalizado) || Path.HasExtension(normalizado) && !normalizado.Contains('/'))
        {
            return pastaUsuario + "/" + arquivo;
        }

        if (normalizado.EndsWith('/') || !Path.HasExtension(normalizado))
        {
            return pastaUsuario + "/" + normalizado.TrimEnd('/') + "/" + arquivo;
        }

        return pastaUsuario + "/" + Path.GetFileName(normalizado);
    }

    public static string NomeOriginal(string? nomeInformado)
    {
        var valor = (nomeInformado ?? string.Empty).Trim();
        return valor.Length <= 500 ? valor : valor[..500];
    }

    public static string NomeDropboxPorId(Guid arquivoId, string? nomeOriginal)
    {
        if (arquivoId == Guid.Empty)
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Identificador do arquivo obrigatorio.");
        }

        var id = arquivoId.ToString("D");
        var original = Path.GetFileName((nomeOriginal ?? string.Empty).Replace('\\', '/')).Trim();
        var extensao = Path.GetExtension(original);
        if (string.IsNullOrWhiteSpace(extensao) || extensao.Length > 20)
        {
            return id;
        }

        var extLimpa = NomeParaDropbox(extensao.TrimStart('.'));
        if (string.IsNullOrWhiteSpace(extLimpa) || extLimpa == "arquivo")
        {
            return id;
        }

        return id + "." + extLimpa.ToLowerInvariant();
    }

    public static string SubstituirNomeArquivo(string? caminhoDropbox, string nomeArquivo)
    {
        var arquivo = NomeParaDropbox(nomeArquivo);
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            throw new DropboxStorageException(DropboxErrorKind.InvalidRequest, "Nome de arquivo invalido.");
        }

        var valor = Normalizar(caminhoDropbox).TrimEnd('/');
        var idx = valor.LastIndexOf('/');
        if (idx <= 0)
        {
            return PastaPadrao + "/" + arquivo;
        }

        return valor[..idx] + "/" + arquivo;
    }

    public static string NomeParaDropbox(string? nomeInformado)
    {
        var arquivo = Path.GetFileName((nomeInformado ?? string.Empty).Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            return string.Empty;
        }

        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(arquivo.Select(c => c < 32 || invalidos.Contains(c) ? '_' : c).ToArray())
            .Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(limpo) || limpo is "." or "..")
        {
            return "arquivo";
        }

        return limpo.Length <= 255 ? limpo : limpo[..255];
    }

    public static string Normalizar(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || caminho.Trim() == "/")
        {
            return string.Empty;
        }

        var valor = caminho.Replace('\\', '/').Trim();
        if (!valor.StartsWith('/'))
        {
            valor = "/" + valor;
        }

        return valor;
    }
}
