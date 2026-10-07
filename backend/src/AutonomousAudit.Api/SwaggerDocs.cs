using System.Text;

namespace AutonomousAudit.Api;

internal static class SwaggerDocs
{
    public static string Bloco(
        string papel,
        string quandoUsar,
        string comoChamar,
        string oQueFaz,
        string resposta,
        string? acompanhamento = null,
        string? atencao = null)
    {
        var texto = new StringBuilder();
        texto.AppendLine("**Papel**");
        texto.AppendLine(papel.Trim());
        texto.AppendLine();
        texto.AppendLine("**Quando usar**");
        texto.AppendLine(quandoUsar.Trim());
        texto.AppendLine();
        texto.AppendLine("**Como chamar**");
        texto.AppendLine(comoChamar.Trim());
        texto.AppendLine();
        texto.AppendLine("**O que a API faz**");
        texto.AppendLine(oQueFaz.Trim());
        texto.AppendLine();
        texto.AppendLine("**Resposta**");
        texto.AppendLine(resposta.Trim());
        if (!string.IsNullOrWhiteSpace(acompanhamento))
        {
            texto.AppendLine();
            texto.AppendLine("**Acompanhamento**");
            texto.AppendLine(acompanhamento.Trim());
        }

        if (!string.IsNullOrWhiteSpace(atencao))
        {
            texto.AppendLine();
            texto.AppendLine("**Atenção**");
            texto.AppendLine(atencao.Trim());
        }

        return texto.ToString().TrimEnd();
    }
}
