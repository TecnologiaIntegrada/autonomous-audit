namespace AutonomousAudit.Domain;

public class Usuario
{
    private readonly List<UsuarioPermissao> _permissoes = [];

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string EmailPrincipal { get; private set; } = string.Empty;
    public string? EmailSecundario { get; private set; }
    public string? TelefonePrincipal { get; private set; }
    public string? TelefoneSecundario { get; private set; }
    public string TipoDocto { get; private set; } = string.Empty;
    public string NDocto { get; private set; } = string.Empty;
    public DateOnly? DataEmissao { get; private set; }
    public string Orgao { get; private set; } = string.Empty;
    public string Cidade { get; private set; } = string.Empty;
    public string Endereco { get; private set; } = string.Empty;
    public string Cep { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;
    public string Pais { get; private set; } = string.Empty;
    public bool SmsAuth { get; private set; }
    public bool EmailAuth { get; private set; }
    public string? SmsAuthCode { get; private set; }
    public string? EmailAuthCode { get; private set; }
    public bool EmailPrincipalVerificado { get; private set; }
    public bool EmailSecundarioVerificado { get; private set; }
    public bool TelefonePrincipalVerificado { get; private set; }
    public bool TelefoneSecundarioVerificado { get; private set; }
    public string? EmailVerificacaoCodigo { get; private set; }
    public string? EmailVerificacaoCanal { get; private set; }
    public DateTimeOffset? EmailVerificacaoExpira { get; private set; }
    public string? SmsVerificacaoCodigo { get; private set; }
    public string? SmsVerificacaoCanal { get; private set; }
    public DateTimeOffset? SmsVerificacaoExpira { get; private set; }
    public string? ApiTokenJti { get; private set; }
    public DateTimeOffset? ApiTokenExpira { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public string? GoogleSub { get; private set; }
    public string? GooglePicture { get; private set; }
    public string? SenhaResetCodigo { get; private set; }
    public DateTimeOffset? SenhaResetExpira { get; private set; }
    public long PerplexityTokensTotal { get; private set; }
    public decimal PerplexityCustoTotalBrl { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    public IReadOnlyCollection<UsuarioPermissao> Permissoes => _permissoes;

    private Usuario()
    {
    }

    public Usuario(
        string nome,
        string emailPrincipal,
        string senhaHash,
        string? emailSecundario = null,
        string? telefonePrincipal = null,
        string? telefoneSecundario = null,
        string? tipoDocto = null,
        string? nDocto = null,
        DateOnly? dataEmissao = null,
        string? orgao = null,
        string? cidade = null,
        string? endereco = null,
        string? cep = null,
        string? uf = null,
        string? pais = null,
        bool smsAuth = false,
        bool emailAuth = false)
    {
        Id = Guid.NewGuid();
        Nome = nome.Trim();
        EmailPrincipal = emailPrincipal.Trim().ToLowerInvariant();
        EmailSecundario = NormalizarOpcional(emailSecundario)?.ToLowerInvariant();
        TelefonePrincipal = TelefoneContato.Normalizar(telefonePrincipal);
        TelefoneSecundario = TelefoneContato.Normalizar(telefoneSecundario);
        TipoDocto = (tipoDocto ?? string.Empty).Trim();
        NDocto = (nDocto ?? string.Empty).Trim();
        DataEmissao = dataEmissao;
        Orgao = (orgao ?? string.Empty).Trim();
        Cidade = (cidade ?? string.Empty).Trim();
        Endereco = (endereco ?? string.Empty).Trim();
        Cep = NormalizarCep(cep);
        Uf = (uf ?? string.Empty).Trim().ToUpperInvariant();
        Pais = string.IsNullOrWhiteSpace(pais) ? "Brasil" : pais.Trim();
        SmsAuth = smsAuth;
        EmailAuth = emailAuth;
        Token = Guid.NewGuid().ToString("N");
        SenhaHash = senhaHash;
        DataCriacao = DateTimeOffset.UtcNow;
        DataAtualizacao = DataCriacao;
    }

    public void Atualizar(
        string nome,
        string emailPrincipal,
        string? emailSecundario,
        string? telefonePrincipal,
        string? telefoneSecundario,
        string? tipoDocto,
        string? nDocto,
        DateOnly? dataEmissao,
        string? orgao,
        string? cidade,
        string? endereco,
        string? cep,
        string? uf,
        string? pais,
        bool smsAuth,
        bool emailAuth)
    {
        Nome = nome.Trim();
        var emailNovo = emailPrincipal.Trim().ToLowerInvariant();
        if (!string.Equals(EmailPrincipal, emailNovo, StringComparison.OrdinalIgnoreCase))
        {
            EmailPrincipalVerificado = false;
        }

        EmailPrincipal = emailNovo;
        DefinirEmailSecundario(emailSecundario);
        DefinirTelefonePrincipal(telefonePrincipal);
        DefinirTelefoneSecundario(telefoneSecundario);
        TipoDocto = (tipoDocto ?? string.Empty).Trim();
        NDocto = (nDocto ?? string.Empty).Trim();
        DataEmissao = dataEmissao;
        Orgao = (orgao ?? string.Empty).Trim();
        Cidade = (cidade ?? string.Empty).Trim();
        Endereco = (endereco ?? string.Empty).Trim();
        Cep = NormalizarCep(cep);
        Uf = (uf ?? string.Empty).Trim().ToUpperInvariant();
        Pais = string.IsNullOrWhiteSpace(pais) ? Pais : pais.Trim();
        SmsAuth = smsAuth;
        EmailAuth = emailAuth;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirSenhaHash(string senhaHash)
    {
        SenhaHash = senhaHash;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void VincularGoogle(string sub, string? picture)
    {
        GoogleSub = string.IsNullOrWhiteSpace(sub) ? GoogleSub : sub.Trim();
        GooglePicture = string.IsNullOrWhiteSpace(picture) ? GooglePicture : picture.Trim();
        EmailPrincipalVerificado = true;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AcumularUsoPerplexity(long tokens, decimal custoBrl)
    {
        if (tokens > 0)
        {
            PerplexityTokensTotal += tokens;
        }

        PerplexityCustoTotalBrl += custoBrl;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AtualizarPerfil(
        string nome,
        string? emailSecundario,
        string? telefonePrincipal,
        string? telefoneSecundario,
        string? cidade,
        string? endereco,
        string? cep,
        string? uf,
        string? pais)
    {
        Nome = nome.Trim();
        DefinirEmailSecundario(emailSecundario);
        DefinirTelefonePrincipal(telefonePrincipal);
        DefinirTelefoneSecundario(telefoneSecundario);
        Cidade = (cidade ?? string.Empty).Trim();
        Endereco = (endereco ?? string.Empty).Trim();
        Cep = NormalizarCep(cep);
        Uf = (uf ?? string.Empty).Trim().ToUpperInvariant();
        Pais = string.IsNullOrWhiteSpace(pais) ? Pais : pais.Trim();
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirEmailSecundario(string? emailSecundario)
    {
        var novo = NormalizarOpcional(emailSecundario)?.ToLowerInvariant();
        if (!string.Equals(EmailSecundario, novo, StringComparison.OrdinalIgnoreCase))
        {
            EmailSecundarioVerificado = false;
            if (string.Equals(EmailVerificacaoCanal, "secundario", StringComparison.OrdinalIgnoreCase))
            {
                EmailVerificacaoCodigo = null;
                EmailVerificacaoCanal = null;
                EmailVerificacaoExpira = null;
            }
        }

        EmailSecundario = novo;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirTelefonePrincipal(string? telefone)
    {
        var novo = TelefoneContato.Normalizar(telefone);
        var atual = TelefoneContato.Normalizar(TelefonePrincipal);
        if (!string.Equals(atual, novo, StringComparison.Ordinal))
        {
            TelefonePrincipalVerificado = false;
            if (string.Equals(SmsVerificacaoCanal, "principal", StringComparison.OrdinalIgnoreCase))
            {
                SmsVerificacaoCodigo = null;
                SmsVerificacaoCanal = null;
                SmsVerificacaoExpira = null;
            }
        }

        TelefonePrincipal = novo;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirTelefoneSecundario(string? telefone)
    {
        var novo = TelefoneContato.Normalizar(telefone);
        var atual = TelefoneContato.Normalizar(TelefoneSecundario);
        if (!string.Equals(atual, novo, StringComparison.Ordinal))
        {
            TelefoneSecundarioVerificado = false;
            if (string.Equals(SmsVerificacaoCanal, "secundario", StringComparison.OrdinalIgnoreCase))
            {
                SmsVerificacaoCodigo = null;
                SmsVerificacaoCanal = null;
                SmsVerificacaoExpira = null;
            }
        }

        TelefoneSecundario = novo;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void IniciarVerificacaoEmail(string canal, string codigo, DateTimeOffset expira)
    {
        EmailVerificacaoCanal = canal;
        EmailVerificacaoCodigo = NormalizarCodigo(codigo);
        EmailVerificacaoExpira = expira;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool ConfirmarVerificacaoEmail(string canal, string? codigo)
    {
        if (!string.Equals(EmailVerificacaoCanal, canal, StringComparison.OrdinalIgnoreCase)
            || EmailVerificacaoExpira is not { } expira
            || expira <= DateTimeOffset.UtcNow
            || !CodigosIguais(EmailVerificacaoCodigo, codigo))
        {
            return false;
        }

        if (string.Equals(canal, "principal", StringComparison.OrdinalIgnoreCase))
        {
            EmailPrincipalVerificado = true;
        }
        else
        {
            EmailSecundarioVerificado = true;
        }

        EmailVerificacaoCodigo = null;
        EmailVerificacaoCanal = null;
        EmailVerificacaoExpira = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        return true;
    }

    public void IniciarVerificacaoSms(string canal, string codigo, DateTimeOffset expira)
    {
        SmsVerificacaoCanal = canal;
        SmsVerificacaoCodigo = NormalizarCodigo(codigo);
        SmsVerificacaoExpira = expira;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool ConfirmarVerificacaoSms(string canal, string? codigo)
    {
        if (!string.Equals(SmsVerificacaoCanal, canal, StringComparison.OrdinalIgnoreCase)
            || SmsVerificacaoExpira is not { } expira
            || expira <= DateTimeOffset.UtcNow
            || !CodigosIguais(SmsVerificacaoCodigo, codigo))
        {
            return false;
        }

        if (string.Equals(canal, "principal", StringComparison.OrdinalIgnoreCase))
        {
            TelefonePrincipalVerificado = true;
        }
        else
        {
            TelefoneSecundarioVerificado = true;
        }

        SmsVerificacaoCodigo = null;
        SmsVerificacaoCanal = null;
        SmsVerificacaoExpira = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
        return true;
    }

    public void RegistrarApiToken(string jti, DateTimeOffset expira)
    {
        ApiTokenJti = jti;
        ApiTokenExpira = expira;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool ApiTokenConfere(string? jti) =>
        !string.IsNullOrWhiteSpace(ApiTokenJti)
        && string.Equals(ApiTokenJti, jti, StringComparison.Ordinal)
        && ApiTokenExpira is { } expira
        && expira > DateTimeOffset.UtcNow;

    public void DefinirResetSenha(string codigo, DateTimeOffset expira)
    {
        SenhaResetCodigo = NormalizarCodigo(codigo);
        SenhaResetExpira = expira;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void LimparResetSenha()
    {
        SenhaResetCodigo = null;
        SenhaResetExpira = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool ResetSenhaConfere(string? informado) =>
        SenhaResetExpira is { } expira
        && expira > DateTimeOffset.UtcNow
        && CodigosIguais(SenhaResetCodigo, informado);

    public void RenovarToken()
    {
        Token = Guid.NewGuid().ToString("N");
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void SubstituirPermissoes(IEnumerable<(Guid ModuloId, Guid RecursoId)> itens)
    {
        _permissoes.Clear();
        foreach (var item in itens.Distinct())
        {
            _permissoes.Add(new UsuarioPermissao(Id, item.ModuloId, item.RecursoId));
        }

        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AdicionarPermissao(Guid moduloId, Guid recursoId)
    {
        if (_permissoes.Any(p => p.RecursoId == recursoId))
        {
            return;
        }

        _permissoes.Add(new UsuarioPermissao(Id, moduloId, recursoId));
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RemoverPermissao(Guid recursoId)
    {
        _permissoes.RemoveAll(p => p.RecursoId == recursoId);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirCodigoSms(string codigo)
    {
        SmsAuthCode = NormalizarCodigo(codigo);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void DefinirCodigoEmail(string codigo)
    {
        EmailAuthCode = NormalizarCodigo(codigo);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void LimparCodigosAuth()
    {
        SmsAuthCode = null;
        EmailAuthCode = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public bool CodigoSmsConfere(string? informado) =>
        CodigosIguais(SmsAuthCode, informado);

    public bool CodigoEmailConfere(string? informado) =>
        CodigosIguais(EmailAuthCode, informado);

    public bool PreencherCadastroIncompleto()
    {
        var alterou = false;
        if (string.IsNullOrWhiteSpace(TelefonePrincipal))
        {
            TelefonePrincipal = "11987654321";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(TelefoneSecundario))
        {
            TelefoneSecundario = "1133334444";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(TipoDocto))
        {
            TipoDocto = "RG";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(NDocto))
        {
            NDocto = "123456789";
            alterou = true;
        }

        if (DataEmissao is null)
        {
            DataEmissao = new DateOnly(2018, 3, 12);
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(Orgao))
        {
            Orgao = "SSP";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(Cidade))
        {
            Cidade = "Sao Paulo";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(Uf))
        {
            Uf = "SP";
            alterou = true;
        }

        if (string.IsNullOrWhiteSpace(Pais))
        {
            Pais = "Brasil";
            alterou = true;
        }

        if (alterou)
        {
            DataAtualizacao = DateTimeOffset.UtcNow;
        }

        return alterou;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarCep(string? cep)
    {
        var valor = (cep ?? string.Empty).Trim();
        if (valor.Length == 8)
        {
            for (var i = 0; i < 8; i++)
            {
                if (!char.IsDigit(valor[i]))
                {
                    return valor;
                }
            }

            return valor[..5] + "-" + valor[5..];
        }

        return valor;
    }

    private static string NormalizarCodigo(string codigo) =>
        new string(codigo.Where(char.IsDigit).ToArray());

    private static bool CodigosIguais(string? salvo, string? informado)
    {
        var a = NormalizarCodigo(salvo ?? string.Empty);
        var b = NormalizarCodigo(informado ?? string.Empty);
        return a.Length == 6 && a.Length == b.Length && string.Equals(a, b, StringComparison.Ordinal);
    }

    public static Usuario Hidratar(
        Guid id,
        string nome,
        string emailPrincipal,
        string? emailSecundario,
        string? telefonePrincipal,
        string? telefoneSecundario,
        string tipoDocto,
        string nDocto,
        DateOnly? dataEmissao,
        string orgao,
        string cidade,
        string endereco,
        string cep,
        string uf,
        string pais,
        bool smsAuth,
        bool emailAuth,
        string token,
        string senhaHash,
        DateTimeOffset dataCriacao,
        DateTimeOffset dataAtualizacao,
        IEnumerable<UsuarioPermissao>? permissoes = null,
        string? googleSub = null,
        string? googlePicture = null,
        string? senhaResetCodigo = null,
        DateTimeOffset? senhaResetExpira = null,
        bool emailPrincipalVerificado = false,
        bool emailSecundarioVerificado = false,
        bool telefonePrincipalVerificado = false,
        bool telefoneSecundarioVerificado = false,
        string? apiTokenJti = null,
        DateTimeOffset? apiTokenExpira = null,
        long perplexityTokensTotal = 0,
        decimal perplexityCustoTotalBrl = 0)
    {
        var usuario = new Usuario
        {
            Id = id,
            Nome = nome,
            EmailPrincipal = emailPrincipal,
            EmailSecundario = emailSecundario,
            TelefonePrincipal = telefonePrincipal,
            TelefoneSecundario = telefoneSecundario,
            TipoDocto = tipoDocto ?? string.Empty,
            NDocto = nDocto ?? string.Empty,
            DataEmissao = dataEmissao,
            Orgao = orgao ?? string.Empty,
            Cidade = cidade ?? string.Empty,
            Endereco = endereco ?? string.Empty,
            Cep = cep ?? string.Empty,
            Uf = uf ?? string.Empty,
            Pais = pais ?? string.Empty,
            SmsAuth = smsAuth,
            EmailAuth = emailAuth,
            Token = token ?? string.Empty,
            SenhaHash = senhaHash ?? string.Empty,
            GoogleSub = googleSub,
            GooglePicture = googlePicture,
            SenhaResetCodigo = senhaResetCodigo,
            SenhaResetExpira = senhaResetExpira,
            EmailPrincipalVerificado = emailPrincipalVerificado,
            EmailSecundarioVerificado = emailSecundarioVerificado,
            TelefonePrincipalVerificado = telefonePrincipalVerificado,
            TelefoneSecundarioVerificado = telefoneSecundarioVerificado,
            ApiTokenJti = apiTokenJti,
            ApiTokenExpira = apiTokenExpira,
            PerplexityTokensTotal = perplexityTokensTotal,
            PerplexityCustoTotalBrl = perplexityCustoTotalBrl,
            DataCriacao = dataCriacao,
            DataAtualizacao = dataAtualizacao
        };
        if (permissoes is not null)
        {
            usuario._permissoes.AddRange(permissoes);
        }

        return usuario;
    }
}
