using System.Security.Cryptography;
using System.Text;

namespace AutonomousAudit.Application.Security;

public static class RecursoChaves
{
    public const string ArquivoCreate = "arquivo.create";
    public const string ArquivoRead = "arquivo.read";

    public const string DocumentoCreate = "documento.create";
    public const string DocumentoRead = "documento.read";
    public const string DocumentoDelete = "documento.delete";
    public const string DocumentoContaRead = "documento.conta.read";

    public const string EmailSend = "email.send";
    public const string EmailContaRead = "email.conta.read";
    public const string EmailContaCreate = "email.conta.create";
    public const string EmailContaUpdate = "email.conta.update";

    public const string SmsSend = "sms.send";
    public const string SmsStatusRead = "sms.status.read";

    public const string PerplexityPrompt = "perplexity.prompt";
    public const string PerplexityOcr = "perplexity.ocr";

    public const string EnderecoRead = "endereco.read";

    public const string UsuarioCreate = "usuario.create";
    public const string UsuarioRead = "usuario.read";
    public const string UsuarioUpdate = "usuario.update";
    public const string UsuarioDelete = "usuario.delete";

    public const string PermissoesAssign = "permissoes.assign";
    public const string PermissoesRead = "permissoes.read";

    public const string ModuloCreate = "modulo.create";
    public const string ModuloRead = "modulo.read";
    public const string ModuloUpdate = "modulo.update";
    public const string ModuloDelete = "modulo.delete";

    public const string RecursoCreate = "recurso.create";
    public const string RecursoRead = "recurso.read";
    public const string RecursoUpdate = "recurso.update";
    public const string RecursoDelete = "recurso.delete";

    public const string ReciboCreate = "recibo.create";
    public const string ReciboRead = "recibo.read";
    public const string ReciboUpdate = "recibo.update";
    public const string ReciboProcessar = "recibo.processar";
    public const string ReciboValidar = "recibo.validar";

    public const string ReciboDelete = "recibo.delete";

    public const string CompraCreate = ReciboCreate;
    public const string CompraRead = ReciboRead;
    public const string CompraUpdate = ReciboUpdate;
    public const string CompraProcessar = ReciboProcessar;
    public const string CompraValidar = ReciboValidar;

    public const string FornecedorCreate = "fornecedor.create";
    public const string FornecedorRead = "fornecedor.read";
    public const string FornecedorUpdate = "fornecedor.update";
    public const string FornecedorDelete = "fornecedor.delete";

    public const string ProdutoCreate = "produto.create";
    public const string ProdutoRead = "produto.read";
    public const string ProdutoUpdate = "produto.update";
    public const string ProdutoDelete = "produto.delete";

    public const string RelatorioRead = "relatorio.read";
}

public sealed record DefinicaoModulo(string Codigo, string Nome, string Descricao);

public sealed record DefinicaoRecurso(
    string ModuloCodigo,
    string RecursoCodigo,
    string Chave,
    string Nome,
    string MetodoHttp,
    string Rota);

public static class CatalogoRecursos
{
    public static IReadOnlyList<DefinicaoModulo> Modulos { get; } =
    [
        new("arquivo", "Arquivos", "Ingest assincrono: NATS ARQUIVOS/dropbox e status no Postgres"),
        new("documento", "Documentos", "Arquivos no Dropbox"),
        new("email", "E-mail", "Envio SMTP via fila NATS, mail_log e contas de clientes"),
        new("sms", "SMS", "Envio e consulta de status na SMSDev"),
        new("perplexity", "Perplexity", "Sonar para pesquisa e Agent API para OCR de recibos"),
        new("endereco", "Enderecos", "Consulta de CEP e logradouro no ViaCEP"),
        new("usuario", "Usuarios", "Cadastro de usuarios do sistema"),
        new("permissoes", "Permissoes", "Atribuicao de recursos a usuarios"),
        new("modulo", "Modulos", "CRUD da tabela modulos"),
        new("recurso", "Recursos", "CRUD da tabela recursos"),
        new("recibo", "Recibos e notas fiscais", "Arquivo do recibo, NF-e ou NFS-e, extracao Perplexity Agent e lancamento em fornecedores/prestadores, produtos e servicos"),
        new("fornecedor", "Fornecedores e prestadores", "Cadastro de fornecedores e prestadores de servico vinculados ao documento extraido"),
        new("produto", "Produtos e servicos", "Cadastro de produtos e servicos extraidos de recibos e notas fiscais"),
        new("relatorio", "Relatorios", "Consulta de fornecedores/prestadores, produtos e servicos por periodo")
    ];

    public static IReadOnlyList<DefinicaoRecurso> Recursos { get; } =
    [
        new("arquivo", "create", RecursoChaves.ArquivoCreate, "Receber arquivo e abrir transacao Dropbox", "POST", "/v1/arquivos"),
        new("arquivo", "read", RecursoChaves.ArquivoRead, "Consultar status do envio ao Dropbox", "GET", "/v1/arquivos/{id}/status"),

        new("documento", "create", RecursoChaves.DocumentoCreate, "Enviar documento ao Dropbox", "POST", "/v1/dropbox/arquivos"),
        new("documento", "read", RecursoChaves.DocumentoRead, "Listar ou pesquisar documentos", "GET", "/v1/dropbox/arquivos"),
        new("documento", "delete", RecursoChaves.DocumentoDelete, "Excluir documento no Dropbox", "DELETE", "/v1/dropbox/arquivos"),
        new("documento", "conta.read", RecursoChaves.DocumentoContaRead, "Consultar conta Dropbox", "GET", "/v1/dropbox/conta"),

        new("email", "send", RecursoChaves.EmailSend, "Enfileirar e consultar envio de e-mail", "POST", "/v1/emails"),
        new("email", "conta.read", RecursoChaves.EmailContaRead, "Listar contas SMTP", "GET", "/v1/emails/contas"),
        new("email", "conta.create", RecursoChaves.EmailContaCreate, "Cadastrar conta SMTP", "POST", "/v1/emails/contas"),
        new("email", "conta.update", RecursoChaves.EmailContaUpdate, "Atualizar senha da conta SMTP", "PUT", "/v1/emails/contas/{id}/senha"),

        new("sms", "send", RecursoChaves.SmsSend, "Enviar SMS", "POST", "/v1/sms"),
        new("sms", "status.read", RecursoChaves.SmsStatusRead, "Consultar status de SMS", "GET", "/v1/sms/{id}"),

        new("perplexity", "prompt", RecursoChaves.PerplexityPrompt, "Consultar Perplexity sonar", "POST", "/v1/perplexity/chat"),
        new("perplexity", "ocr", RecursoChaves.PerplexityOcr, "OCR Perplexity Agent sincrono ou assincrono", "POST", "/v1/perplexity/sync"),

        new("endereco", "read", RecursoChaves.EnderecoRead, "Consultar endereco no ViaCEP", "GET", "/v1/enderecos/cep/{cep}"),

        new("usuario", "create", RecursoChaves.UsuarioCreate, "Cadastrar usuario", "POST", "/v1/usuarios"),
        new("usuario", "read", RecursoChaves.UsuarioRead, "Consultar usuarios", "GET", "/v1/usuarios"),
        new("usuario", "update", RecursoChaves.UsuarioUpdate, "Atualizar usuario", "PUT", "/v1/usuarios/{id}"),
        new("usuario", "delete", RecursoChaves.UsuarioDelete, "Excluir usuario", "DELETE", "/v1/usuarios/{id}"),

        new("permissoes", "assign", RecursoChaves.PermissoesAssign, "Atribuir ou revogar permissoes", "PUT", "/v1/usuarios/{id}/permissoes"),
        new("permissoes", "read", RecursoChaves.PermissoesRead, "Listar permissoes do usuario", "GET", "/v1/usuarios/{id}/permissoes"),

        new("modulo", "create", RecursoChaves.ModuloCreate, "Cadastrar modulo", "POST", "/v1/modulos"),
        new("modulo", "read", RecursoChaves.ModuloRead, "Consultar modulos", "GET", "/v1/modulos"),
        new("modulo", "update", RecursoChaves.ModuloUpdate, "Atualizar modulo", "PUT", "/v1/modulos/{id}"),
        new("modulo", "delete", RecursoChaves.ModuloDelete, "Excluir modulo", "DELETE", "/v1/modulos/{id}"),

        new("recurso", "create", RecursoChaves.RecursoCreate, "Cadastrar recurso", "POST", "/v1/recursos"),
        new("recurso", "read", RecursoChaves.RecursoRead, "Consultar recursos", "GET", "/v1/recursos"),
        new("recurso", "update", RecursoChaves.RecursoUpdate, "Atualizar recurso", "PUT", "/v1/recursos/{id}"),
        new("recurso", "delete", RecursoChaves.RecursoDelete, "Excluir recurso", "DELETE", "/v1/recursos/{id}"),

        new("recibo", "create", RecursoChaves.ReciboCreate, "Enviar recibo (imagens ou PDF ate 50 MB), enfileirar o Perplexity Agent e limpar rascunhos de dias anteriores", "POST", "/v1/recibos"),
        new("recibo", "read", RecursoChaves.ReciboRead, "Consultar recibos, JSON extraido e resumo do dashboard", "GET", "/v1/recibos"),
        new("recibo", "update", RecursoChaves.ReciboUpdate, "Alterar anexos, sessao de captura e fornecedor do recibo", "PATCH", "/v1/recibos/{id}/anexos"),
        new("recibo", "processar", RecursoChaves.ReciboProcessar, "Enfileirar processamento Perplexity/Dropbox", "POST", "/v1/recibos/{id}/processar"),
        new("recibo", "validar", RecursoChaves.ReciboValidar, "Recurso legado de validacao (nao usado no fluxo atual)", "PUT", "/v1/recibos/{id}/validacao"),

        new("recibo", "delete", RecursoChaves.ReciboDelete, "Excluir recibo/NF com compra, arquivos, produtos exclusivos e fornecedor sem outros recibos", "DELETE", "/v1/recibos/{id}"),

        new("fornecedor", "create", RecursoChaves.FornecedorCreate, "Cadastrar fornecedor ou prestador", "POST", "/v1/fornecedores"),
        new("fornecedor", "read", RecursoChaves.FornecedorRead, "Consultar fornecedores, prestadores e recibos vinculados", "GET", "/v1/fornecedores"),
        new("fornecedor", "update", RecursoChaves.FornecedorUpdate, "Atualizar fornecedor ou prestador", "PUT", "/v1/fornecedores/{id}"),
        new("fornecedor", "delete", RecursoChaves.FornecedorDelete, "Excluir fornecedor ou prestador sem compras vinculadas", "DELETE", "/v1/fornecedores/{id}"),

        new("produto", "create", RecursoChaves.ProdutoCreate, "Cadastrar produto ou servico", "POST", "/v1/produtos"),
        new("produto", "read", RecursoChaves.ProdutoRead, "Consultar produtos e servicos", "GET", "/v1/produtos"),
        new("produto", "update", RecursoChaves.ProdutoUpdate, "Atualizar produto ou servico", "PUT", "/v1/produtos/{id}"),
        new("produto", "delete", RecursoChaves.ProdutoDelete, "Excluir produto ou servico sem itens de compra vinculados", "DELETE", "/v1/produtos/{id}"),

        new("relatorio", "read", RecursoChaves.RelatorioRead, "Consultar relatorio de compras, fornecedores/prestadores, produtos e servicos (periodo maximo 3 meses, ate 10000 registros, json/csv/xlsx)", "GET", "/v1/relatorios/itens")
    ];

    public static Guid IdModulo(string codigo) => IdEstavel("modulo:" + codigo);

    public static Guid IdRecurso(string chave) => IdEstavel("recurso:" + chave);

    private static Guid IdEstavel(string valor)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("autonomousaudit:" + valor));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }
}
