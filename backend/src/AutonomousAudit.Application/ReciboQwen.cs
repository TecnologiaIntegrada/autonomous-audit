using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace AutonomousAudit.Application;

public static class ReciboQwenPrompt
{
    public const string Versao = "recibo-nf-v2";

    public const string Texto =
        """
        Voce e um extrator de recibos, comprovantes de compra, notas fiscais eletrônicas (DANFE/NF-e) e notas fiscais de serviço (NFS-e).
        Responda APENAS com JSON valido, sem markdown. Nunca invente valores. Se um campo nao estiver legivel, use null.

        O documento e um PDF montado a partir de uma ou mais imagens/paginas, na ordem do anexo.
        Cada imagem enviada corresponde a uma pagina, numerada a partir de 1.

        Classificacao:
        - tipoDocumento = "nota_fiscal" quando houver DANFE, NF-e, NFS-e, chave de acesso de 44 digitos, protocolo de autorizacao, layout de nota fiscal, ou os blocos PRESTADOR DE SERVICOS / TOMADOR DE SERVICOS.
        - tipoDocumento = "recibo" para cupom, comprovante de compra ou recibo simples.
        - tipoItem = "produto", "servico" ou "misto" conforme os itens. Cada item tambem tem tipo "produto" ou "servico". NFS-e e nota com PRESTADOR/TOMADOR = tipoItem "servico".

        Partes da nota:
        - Recibo: "fornecedor" e quem emitiu o comprovante.
        - DANFE/NF-e de mercadoria: "fornecedor" = emitente/remetente; "nfe.destinatario" = destinatario.
        - NFS-e / nota de servico: "fornecedor" = PRESTADOR DE SERVICOS; "nfe.destinatario" = TOMADOR DE SERVICOS (nome/razao, CPF/CNPJ, endereco, inscricao municipal no campo inscricaoEstadual).

        Condicionais:
        - Se cada imagem/pagina for um documento diferente, emita um objeto em "recibos" para cada uma.
        - Se um mesmo documento ocupar duas ou mais imagens, agrupe em UM objeto com "paginas".
        - Nao misture itens, totais ou fornecedores/prestadores de documentos diferentes.

        Schema:
        {
          "recibos": [
            {
              "paginas": [1],
              "tipoDocumento": "recibo"|"nota_fiscal",
              "tipoItem": "produto"|"servico"|"misto",
              "fornecedor": { "nome": string|null, "razaoSocial": string|null, "cpfCnpj": string|null, "endereco": string|null, "telefone": string|null, "inscricaoEstadual": string|null },
              "compra": {
                "data": string|null, "numeroRecibo": string|null, "subtotal": number|null, "descontos": number|null,
                "acrescimos": number|null, "total": number|null, "formaPagamento": string|null,
                "danfeTipo": 0|1|null, "serie": string|null, "folha": string|null, "chaveAcesso": string|null,
                "codigoBarras": string|null, "protocoloAutorizacao": string|null, "protocoloData": string|null,
                "naturezaOperacao": string|null, "inscricaoEstadual": string|null, "inscricaoEstadualSt": string|null,
                "dataEmissao": string|null
              },
              "nfe": {
                "destinatario": {
                  "nomeRazaoSocial": string|null, "cpfCnpj": string|null, "endereco": string|null, "bairro": string|null,
                  "cep": string|null, "municipio": string|null, "uf": string|null, "telefone": string|null,
                  "inscricaoEstadual": string|null, "dataEmissao": string|null, "dataSaida": string|null, "horaSaida": string|null
                },
                "imposto": {
                  "baseIcms": number|null, "valorIcms": number|null, "baseIcmsSt": number|null, "valorIcmsSt": number|null,
                  "valorTotalProdutos": number|null, "valorFrete": number|null, "valorSeguro": number|null,
                  "desconto": number|null, "outrasDespesas": number|null, "valorIpi": number|null, "valorTotalNota": number|null
                },
                "transportador": {
                  "nomeRazaoSocial": string|null, "fretePorConta": string|null, "codigoAntt": string|null, "placa": string|null,
                  "uf": string|null, "cpfCnpj": string|null, "endereco": string|null, "municipio": string|null,
                  "ufEndereco": string|null, "inscricaoEstadual": string|null, "quantidadeVolumes": number|null,
                  "especie": string|null, "marca": string|null, "numeracao": string|null, "pesoBruto": number|null, "pesoLiquido": number|null
                },
                "adicionais": {
                  "informacoesComplementares": string|null, "reservadoAoFisco": string|null, "dataHoraImpressao": string|null
                }
              },
              "itens": [
                {
                  "tipo": "produto"|"servico", "descricao": string, "codigo": string|null, "quantidade": number|null,
                  "unidade": string|null, "precoUnitario": number|null, "desconto": number|null, "total": number|null,
                  "valorLiquido": number|null, "marca": string|null, "variante": string|null,
                  "ncmSh": string|null, "csosn": string|null, "cfop": string|null,
                  "baseIcms": number|null, "valorIcms": number|null, "valorIpi": number|null, "aliqIcms": number|null, "aliqIpi": number|null
                }
              ]
            }
          ]
        }
        Use nfe para nota_fiscal: em mercadoria, destinatario/imposto/transportador; em NFS-e, nfe.destinatario e o TOMADOR e fornecedor e o PRESTADOR.
        danfeTipo: 0 = Entrada, 1 = Saida (so DANFE). chaveAcesso so com digitos (44).
        """;
}

public sealed class ReciboQwenLoteDto
{
    public List<ReciboQwenDto>? Recibos { get; set; }
    public List<ReciboQwenDto>? Compras { get; set; }
}

public sealed class ReciboQwenDto
{
    public List<int>? Paginas { get; set; }
    public List<int>? Imagens { get; set; }
    public string? TipoDocumento { get; set; }
    public string? TipoItem { get; set; }
    public ReciboQwenFornecedorDto? Fornecedor { get; set; }
    public ReciboQwenCompraDto? Compra { get; set; }
    public ReciboQwenNfeDto? Nfe { get; set; }
    public List<ReciboQwenItemDto> Itens { get; set; } = [];

    public IReadOnlyList<int> PaginasResolvidas
    {
        get
        {
            if (Paginas is { Count: > 0 })
            {
                return Paginas;
            }

            if (Imagens is { Count: > 0 })
            {
                return Imagens;
            }

            return [];
        }
    }
}

public sealed class ReciboQwenFornecedorDto
{
    public string? Nome { get; set; }
    public string? RazaoSocial { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Endereco { get; set; }
    public string? Telefone { get; set; }
    public string? InscricaoEstadual { get; set; }
}

public sealed class ReciboQwenCompraDto
{
    public string? Data { get; set; }
    public string? NumeroRecibo { get; set; }
    public string? Numero { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Descontos { get; set; }
    public decimal? Acrescimos { get; set; }
    public decimal? Total { get; set; }
    public string? FormaPagamento { get; set; }
    public int? DanfeTipo { get; set; }
    public string? Serie { get; set; }
    public string? Folha { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? CodigoBarras { get; set; }
    public string? ProtocoloAutorizacao { get; set; }
    public string? ProtocoloData { get; set; }
    public string? NaturezaOperacao { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoEstadualSt { get; set; }
    public string? DataEmissao { get; set; }

    public string? NumeroReciboResolvido =>
        string.IsNullOrWhiteSpace(NumeroRecibo) ? Numero : NumeroRecibo;
}

public sealed class ReciboQwenNfeDto
{
    public ReciboQwenNfeDestinatarioDto? Destinatario { get; set; }
    public ReciboQwenNfeImpostoDto? Imposto { get; set; }
    public ReciboQwenNfeTransportadorDto? Transportador { get; set; }
    public ReciboQwenNfeAdicionaisDto? Adicionais { get; set; }
}

public sealed class ReciboQwenNfeDestinatarioDto
{
    public string? NomeRazaoSocial { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Endereco { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
    public string? Municipio { get; set; }
    public string? Uf { get; set; }
    public string? Telefone { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? DataEmissao { get; set; }
    public string? DataSaida { get; set; }
    public string? HoraSaida { get; set; }
}

public sealed class ReciboQwenNfeImpostoDto
{
    public decimal? BaseIcms { get; set; }
    public decimal? ValorIcms { get; set; }
    public decimal? BaseIcmsSt { get; set; }
    public decimal? ValorIcmsSt { get; set; }
    public decimal? ValorTotalProdutos { get; set; }
    public decimal? ValorFrete { get; set; }
    public decimal? ValorSeguro { get; set; }
    public decimal? Desconto { get; set; }
    public decimal? OutrasDespesas { get; set; }
    public decimal? ValorIpi { get; set; }
    public decimal? ValorTotalNota { get; set; }
}

public sealed class ReciboQwenNfeTransportadorDto
{
    public string? NomeRazaoSocial { get; set; }
    public string? FretePorConta { get; set; }
    public string? CodigoAntt { get; set; }
    public string? Placa { get; set; }
    public string? Uf { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Endereco { get; set; }
    public string? Municipio { get; set; }
    public string? UfEndereco { get; set; }
    public string? InscricaoEstadual { get; set; }
    public decimal? QuantidadeVolumes { get; set; }
    public string? Especie { get; set; }
    public string? Marca { get; set; }
    public string? Numeracao { get; set; }
    public decimal? PesoBruto { get; set; }
    public decimal? PesoLiquido { get; set; }
}

public sealed class ReciboQwenNfeAdicionaisDto
{
    public string? InformacoesComplementares { get; set; }
    public string? ReservadoAoFisco { get; set; }
    public string? DataHoraImpressao { get; set; }
}

public sealed class ReciboQwenItemDto
{
    public string? Descricao { get; set; }
    public string? Codigo { get; set; }
    public decimal? Quantidade { get; set; }
    public string? Unidade { get; set; }
    public decimal? PrecoUnitario { get; set; }
    public decimal? Desconto { get; set; }
    public decimal? Total { get; set; }
    public string? Marca { get; set; }
    public string? Variante { get; set; }
    public string? Tipo { get; set; }
    public string? NcmSh { get; set; }
    public string? Csosn { get; set; }
    public string? Cfop { get; set; }
    public decimal? ValorLiquido { get; set; }
    public decimal? BaseIcms { get; set; }
    public decimal? ValorIcms { get; set; }
    public decimal? ValorIpi { get; set; }
    public decimal? AliqIcms { get; set; }
    public decimal? AliqIpi { get; set; }
}

public sealed class ReciboQwenDtoValidator : AbstractValidator<ReciboQwenDto>
{
    public ReciboQwenDtoValidator()
    {
        RuleFor(x => x.Itens).NotNull();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty();
        });
    }
}

public static class ReciboQwenParser
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static ReciboQwenDto? TentarParsear(string? texto, out string jsonLimpo)
    {
        var lote = TentarParsearLote(texto, out jsonLimpo);
        return lote.Count == 0 ? null : lote[0];
    }

    public static IReadOnlyList<ReciboQwenDto> TentarParsearLote(string? texto, out string jsonLimpo)
    {
        jsonLimpo = string.Empty;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var bruto = texto.Trim();
        var inicioObj = bruto.IndexOf('{');
        var inicioArr = bruto.IndexOf('[');
        if (inicioArr >= 0 && (inicioObj < 0 || inicioArr < inicioObj))
        {
            var fimArr = bruto.LastIndexOf(']');
            if (fimArr <= inicioArr)
            {
                return [];
            }

            jsonLimpo = bruto[inicioArr..(fimArr + 1)];
            try
            {
                var lista = JsonSerializer.Deserialize<List<ReciboQwenDto>>(jsonLimpo, Json);
                return lista is { Count: > 0 } ? lista : [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        var fim = bruto.LastIndexOf('}');
        if (inicioObj < 0 || fim <= inicioObj)
        {
            return [];
        }

        jsonLimpo = bruto[inicioObj..(fim + 1)];
        try
        {
            var lote = JsonSerializer.Deserialize<ReciboQwenLoteDto>(jsonLimpo, Json);
            if (lote?.Recibos is { Count: > 0 })
            {
                return lote.Recibos;
            }

            if (lote?.Compras is { Count: > 0 })
            {
                return lote.Compras;
            }

            var unico = JsonSerializer.Deserialize<ReciboQwenDto>(jsonLimpo, Json);
            if (unico is null)
            {
                return [];
            }

            var temDados = unico.Fornecedor is not null
                || unico.Compra is not null
                || unico.Itens.Count > 0
                || unico.PaginasResolvidas.Count > 0;
            return temDados ? [unico] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string PaginasTexto(IReadOnlyList<int> paginas) =>
        paginas.Count == 0 ? string.Empty : string.Join(",", paginas);
}
