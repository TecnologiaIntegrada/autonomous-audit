using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AutonomousAudit.Api;

public sealed class SwaggerTagDescriptionsFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags =
        [
            Tag(
                "Autenticacao",
                "Entrada no sistema. Login por e-mail/senha, Google (id_token) e recuperacao de senha por e-mail. " +
                "O JWT de 1 hora e usado nas demais rotas. " +
                "Se o cadastro exigir MFA por SMS ou e-mail, o token só sai depois do código de confirmação."),
            Tag(
                "Usuarios",
                "Cadastro das pessoas que acessam a API. Inclui dados pessoais, flags de MFA e a senha (sempre armazenada como hash). " +
                "A senha nunca retorna nas respostas. " +
                "Quem tem o recurso do catalogo so opera o proprio usuario; para agir sobre outros o ID precisa estar em ADM_SYS."),
            Tag(
                "Administradores",
                "Tabela adm_sys (id_admin, id_usuario): usuarios que podem listar, criar e alterar qualquer cadastro em /v1/usuarios. " +
                "So quem ja esta em ADM_SYS gerencia esta lista."),
            Tag(
                "Modulos",
                "Agrupadores do catálogo de autorização (ex.: e-mail, arquivos, Perplexity). " +
                "Cada módulo reúne os recursos que depois são atribuídos aos usuários."),
            Tag(
                "Recursos",
                "Ações autorizáveis no formato modulo.acao (ex.: email.send). " +
                "Sem o recurso correspondente no perfil, a rota responde 403 mesmo com JWT válido."),
            Tag(
                "Arquivos",
                "Ingestão assíncrona de documentos. O POST aceita o arquivo, devolve um id na hora e envia o binário ao Dropbox em segundo plano via NATS."),
            Tag(
                "Dropbox",
                "Operações síncronas na conta Dropbox da aplicação: consultar a conta, listar/pesquisar, enviar e excluir arquivos na pasta /AutonomousAudit."),
            Tag(
                "E-mail",
                "Envio SMTP pela conta cadastrada em mail_accounts. Os POSTs só enfileiram (201); um consumer NATS despacha a mensagem. " +
                "O GET consulta o andamento em mail_log. Anexos são temporários e não ficam no servidor."),
            Tag(
                "Perplexity",
                "Perguntas síncronas ao modelo sonar da Perplexity, com resposta e citações quando a provedora devolver fontes."),
            Tag(
                "SMS",
                "Envio e consulta de SMS pela SMSDev. O POST dispara a mensagem na hora; o GET consulta o status de entrega (DLR) pelo id devolvido."),
            Tag(
                "Enderecos",
                "Consulta de endereços brasileiros no ViaCEP: CEP único ou pesquisa por UF, cidade e logradouro. Sem API key. No máximo 50 itens na pesquisa."),
            Tag(
                "Recibos",
                "Recibos de compras: envio de imagens ou PDF (ate 50 MB), fila Perplexity Agent e lancamento automatico em fornecedores e produtos. " +
                "O recibo so e gravado quando ha arquivo. Cada recibo pertence ao usuario do JWT; ADM_SYS pode ver qualquer um. A captura publica nao exige JWT."),
            Tag(
                "Fornecedores",
                "Cadastro de fornecedores e prestadores de servico extraidos de recibos, NF-e e NFS-e."),
            Tag(
                "Produtos",
                "Cadastro de produtos vinculados ao codigo do recibo extraido pelo Perplexity Agent."),
            Tag(
                "Saude",
                "Probes de liveness/readiness do container. Não exigem JWT. Usados pelo Kubernetes e por monitoramento.")
        ];
    }

    private static OpenApiTag Tag(string nome, string descricao) =>
        new() { Name = nome, Description = descricao };
}
