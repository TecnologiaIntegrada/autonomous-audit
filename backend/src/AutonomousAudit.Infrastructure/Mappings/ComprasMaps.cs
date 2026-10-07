using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class FornecedorMap : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> builder)
    {
        builder.ToTable("fornecedores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RazaoSocial).HasColumnName("razao_social").HasMaxLength(300);
        builder.Property(x => x.CpfCnpj).HasColumnName("cpf_cnpj").HasMaxLength(20);
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(x => x.Endereco).HasColumnName("endereco").HasMaxLength(500);
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();

        builder.HasIndex(x => new { x.UsuarioId, x.CpfCnpj })
            .IsUnique()
            .HasFilter("cpf_cnpj IS NOT NULL");
        builder.HasIndex(x => new { x.UsuarioId, x.NomeNormalizado }).IsUnique();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProdutoMap : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.ReciboId).HasColumnName("recibo_id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Marca).HasColumnName("marca").HasMaxLength(120);
        builder.Property(x => x.Variante).HasColumnName("variante").HasMaxLength(120);
        builder.Property(x => x.UnidadeControle).HasColumnName("unidade_controle").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ConteudoEmbalagem).HasColumnName("conteudo_embalagem").HasColumnType("numeric(18,4)");
        builder.Property(x => x.CodigoExterno).HasColumnName("codigo_externo").HasMaxLength(80);
        builder.Property(x => x.NcmSh).HasColumnName("ncm_sh").HasMaxLength(20);
        builder.Property(x => x.Csosn).HasColumnName("csosn").HasMaxLength(20);
        builder.Property(x => x.Cfop).HasColumnName("cfop").HasMaxLength(20);
        builder.Property(x => x.ValorUnitario).HasColumnName("valor_unitario").HasColumnType("numeric(18,4)");
        builder.Property(x => x.ValorDesconto).HasColumnName("valor_desconto").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorLiquido).HasColumnName("valor_liquido").HasColumnType("numeric(18,2)");
        builder.Property(x => x.BaseIcms).HasColumnName("base_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIcms).HasColumnName("valor_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIpi).HasColumnName("valor_ipi").HasColumnType("numeric(18,2)");
        builder.Property(x => x.AliqIcms).HasColumnName("aliq_icms").HasColumnType("numeric(8,4)");
        builder.Property(x => x.AliqIpi).HasColumnName("aliq_ipi").HasColumnType("numeric(8,4)");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();

        builder.HasIndex(x => new { x.UsuarioId, x.NomeNormalizado });
        builder.HasIndex(x => x.ReciboId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Compra>().WithMany().HasForeignKey(x => x.ReciboId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Nomes)
            .WithOne()
            .HasForeignKey(x => x.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Nomes).HasField("_nomes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ProdutoNomeMap : IEntityTypeConfiguration<ProdutoNome>
{
    public void Configure(EntityTypeBuilder<ProdutoNome> builder)
    {
        builder.ToTable("produto_nomes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ProdutoId).HasColumnName("produto_id").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.ProdutoId, x.NomeNormalizado }).IsUnique();
    }
}

public class CompraMap : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> builder)
    {
        builder.ToTable("compras");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.FornecedorId).HasColumnName("fornecedor_id");
        builder.Property(x => x.ReciboOrigemId).HasColumnName("recibo_origem_id");
        builder.Property(x => x.Paginas).HasColumnName("paginas").HasMaxLength(80);
        builder.Property(x => x.DataEnvio).HasColumnName("data_envio").IsRequired();
        builder.Property(x => x.DataCompra).HasColumnName("data_compra");
        builder.Property(x => x.NumeroRecibo).HasColumnName("numero_recibo").HasMaxLength(80);
        builder.Property(x => x.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20).IsRequired().HasDefaultValue(CompraTipoDocumento.Recibo);
        builder.Property(x => x.TipoItem).HasColumnName("tipo_item").HasMaxLength(20).IsRequired().HasDefaultValue(CompraTipoItem.Produto);
        builder.Property(x => x.DanfeTipo).HasColumnName("danfe_tipo");
        builder.Property(x => x.Serie).HasColumnName("serie").HasMaxLength(20);
        builder.Property(x => x.Folha).HasColumnName("folha").HasMaxLength(20);
        builder.Property(x => x.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(60);
        builder.Property(x => x.CodigoBarras).HasColumnName("codigo_barras").HasMaxLength(80);
        builder.Property(x => x.ProtocoloAutorizacao).HasColumnName("protocolo_autorizacao").HasMaxLength(80);
        builder.Property(x => x.ProtocoloData).HasColumnName("protocolo_data");
        builder.Property(x => x.NaturezaOperacao).HasColumnName("natureza_operacao").HasMaxLength(200);
        builder.Property(x => x.InscricaoEstadual).HasColumnName("inscricao_estadual").HasMaxLength(30);
        builder.Property(x => x.InscricaoEstadualSt).HasColumnName("inscricao_estadual_st").HasMaxLength(30);
        builder.Property(x => x.DataEmissao).HasColumnName("data_emissao");
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Descontos).HasColumnName("descontos").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Acrescimos).HasColumnName("acrescimos").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Total).HasColumnName("total").HasColumnType("numeric(18,2)");
        builder.Property(x => x.FormaPagamento).HasColumnName("forma_pagamento").HasMaxLength(80);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.HashArquivo).HasColumnName("hash_arquivo").HasMaxLength(64);
        builder.Property(x => x.QwenJsonBruto).HasColumnName("qwen_json_bruto");
        builder.Property(x => x.DivergenciasJson).HasColumnName("divergencias_json");
        builder.Property(x => x.ChaveDuplicidade).HasColumnName("chave_duplicidade").HasMaxLength(300);
        builder.Property(x => x.TentativasProcessamento).HasColumnName("tentativas_processamento").IsRequired();
        builder.Property(x => x.UltimoErro).HasColumnName("ultimo_erro").HasMaxLength(2000);
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.Property(x => x.ProcessadoEm).HasColumnName("processado_em");
        builder.Property(x => x.ValidadoEm).HasColumnName("validado_em");
        builder.Property(x => x.ConcluidoEm).HasColumnName("concluido_em");

        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.HashArquivo);
        builder.HasIndex(x => x.ReciboOrigemId);
        builder.HasIndex(x => new { x.UsuarioId, x.ChaveDuplicidade });
        builder.HasIndex(x => x.ChaveAcesso);
        builder.HasIndex(x => x.TipoDocumento);

        builder.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.NfeDestinatario).WithOne().HasForeignKey<CompraNfeDestinatario>(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.NfeImposto).WithOne().HasForeignKey<CompraNfeImposto>(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.NfeTransportador).WithOne().HasForeignKey<CompraNfeTransportador>(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.NfeAdicionais).WithOne().HasForeignKey<CompraNfeAdicionais>(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Compra>().WithMany().HasForeignKey(x => x.ReciboOrigemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Anexos).WithOne().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documentos).WithOne().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Capturas).WithOne().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Anexos).HasField("_anexos").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Documentos).HasField("_documentos").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Capturas).HasField("_capturas").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class CompraItemMap : IEntityTypeConfiguration<CompraItem>
{
    public void Configure(EntityTypeBuilder<CompraItem> builder)
    {
        builder.ToTable("compra_itens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CompraId).HasColumnName("compra_id").IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.DescricaoOriginal).HasColumnName("descricao_original").HasMaxLength(400).IsRequired();
        builder.Property(x => x.CodigoImpresso).HasColumnName("codigo_impresso").HasMaxLength(80);
        builder.Property(x => x.Quantidade).HasColumnName("qtd").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Unidade).HasColumnName("unidade").HasMaxLength(20);
        builder.Property(x => x.PrecoUnitario).HasColumnName("preco_unitario").HasColumnType("numeric(18,4)");
        builder.Property(x => x.Desconto).HasColumnName("desconto").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Total).HasColumnName("total").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ProdutoId).HasColumnName("produto_id");
        builder.Property(x => x.ServicoId).HasColumnName("servico_id");
        builder.Property(x => x.TipoItem).HasColumnName("tipo_item").HasMaxLength(20).IsRequired().HasDefaultValue(CompraTipoItem.Produto);
        builder.Property(x => x.NcmSh).HasColumnName("ncm_sh").HasMaxLength(20);
        builder.Property(x => x.Csosn).HasColumnName("csosn").HasMaxLength(20);
        builder.Property(x => x.Cfop).HasColumnName("cfop").HasMaxLength(20);
        builder.Property(x => x.ValorLiquido).HasColumnName("valor_liquido").HasColumnType("numeric(18,2)");
        builder.Property(x => x.BaseIcms).HasColumnName("base_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIcms).HasColumnName("valor_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIpi).HasColumnName("valor_ipi").HasColumnType("numeric(18,2)");
        builder.Property(x => x.AliqIcms).HasColumnName("aliq_icms").HasColumnType("numeric(8,4)");
        builder.Property(x => x.AliqIpi).HasColumnName("aliq_ipi").HasColumnType("numeric(8,4)");

        builder.HasIndex(x => x.CompraId);
        builder.HasIndex(x => x.ServicoId);
        builder.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Servico).WithMany().HasForeignKey(x => x.ServicoId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CompraAnexoMap : IEntityTypeConfiguration<CompraAnexo>
{
    public void Configure(EntityTypeBuilder<CompraAnexo> builder)
    {
        builder.ToTable("compra_anexos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CompraId).HasColumnName("compra_id").IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Mime).HasColumnName("mime").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
        builder.Property(x => x.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CaminhoLocal).HasColumnName("caminho_local").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.TamanhoBytes).HasColumnName("tamanho_bytes").IsRequired();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.HasIndex(x => new { x.CompraId, x.Ordem });
    }
}

public class CompraDocumentoMap : IEntityTypeConfiguration<CompraDocumento>
{
    public void Configure(EntityTypeBuilder<CompraDocumento> builder)
    {
        builder.ToTable("compra_documentos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CompraId).HasColumnName("compra_id").IsRequired();
        builder.Property(x => x.DropboxPath).HasColumnName("dropbox_path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.DropboxId).HasColumnName("dropbox_id").HasMaxLength(80);
        builder.Property(x => x.DropboxDispatchId).HasColumnName("dropbox_dispatch_id");
        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CaminhoLocalPdf).HasColumnName("caminho_local_pdf").HasMaxLength(1000);
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.HasIndex(x => x.CompraId);
    }
}

public class CapturaSessaoMap : IEntityTypeConfiguration<CapturaSessao>
{
    public void Configure(EntityTypeBuilder<CapturaSessao> builder)
    {
        builder.ToTable("captura_sessoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CompraId).HasColumnName("compra_id").IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SessionTokenHash).HasColumnName("session_token_hash").HasMaxLength(64);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(x => x.ConsumidoEm).HasColumnName("consumido_em");
        builder.Property(x => x.EncerradoEm).HasColumnName("encerrado_em");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.SessionTokenHash).IsUnique().HasFilter("session_token_hash IS NOT NULL");
        builder.HasIndex(x => x.CompraId);
    }
}

public class ServicoMap : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> builder)
    {
        builder.ToTable("servicos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.ReciboId).HasColumnName("recibo_id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CodigoExterno).HasColumnName("codigo_externo").HasMaxLength(80);
        builder.Property(x => x.UnidadeControle).HasColumnName("unidade_controle").HasMaxLength(20).IsRequired();
        builder.Property(x => x.NcmSh).HasColumnName("ncm_sh").HasMaxLength(20);
        builder.Property(x => x.Csosn).HasColumnName("csosn").HasMaxLength(20);
        builder.Property(x => x.Cfop).HasColumnName("cfop").HasMaxLength(20);
        builder.Property(x => x.ValorUnitario).HasColumnName("valor_unitario").HasColumnType("numeric(18,4)");
        builder.Property(x => x.ValorDesconto).HasColumnName("valor_desconto").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorLiquido).HasColumnName("valor_liquido").HasColumnType("numeric(18,2)");
        builder.Property(x => x.BaseIcms).HasColumnName("base_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIcms).HasColumnName("valor_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIpi).HasColumnName("valor_ipi").HasColumnType("numeric(18,2)");
        builder.Property(x => x.AliqIcms).HasColumnName("aliq_icms").HasColumnType("numeric(8,4)");
        builder.Property(x => x.AliqIpi).HasColumnName("aliq_ipi").HasColumnType("numeric(8,4)");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.HasIndex(x => new { x.UsuarioId, x.NomeNormalizado });
        builder.HasIndex(x => x.ReciboId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Compra>().WithMany().HasForeignKey(x => x.ReciboId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CompraNfeDestinatarioMap : IEntityTypeConfiguration<CompraNfeDestinatario>
{
    public void Configure(EntityTypeBuilder<CompraNfeDestinatario> builder)
    {
        builder.ToTable("compra_nfe_destinatarios");
        builder.HasKey(x => x.CompraId);
        builder.Property(x => x.CompraId).HasColumnName("compra_id").ValueGeneratedNever();
        builder.Property(x => x.NomeRazaoSocial).HasColumnName("nome_razao_social").HasMaxLength(300);
        builder.Property(x => x.CpfCnpj).HasColumnName("cpf_cnpj").HasMaxLength(20);
        builder.Property(x => x.Endereco).HasColumnName("endereco").HasMaxLength(500);
        builder.Property(x => x.Bairro).HasColumnName("bairro").HasMaxLength(120);
        builder.Property(x => x.Cep).HasColumnName("cep").HasMaxLength(12);
        builder.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(120);
        builder.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(x => x.InscricaoEstadual).HasColumnName("inscricao_estadual").HasMaxLength(30);
        builder.Property(x => x.DataEmissao).HasColumnName("data_emissao");
        builder.Property(x => x.DataSaida).HasColumnName("data_saida");
        builder.Property(x => x.HoraSaida).HasColumnName("hora_saida").HasMaxLength(20);
    }
}

public class CompraNfeImpostoMap : IEntityTypeConfiguration<CompraNfeImposto>
{
    public void Configure(EntityTypeBuilder<CompraNfeImposto> builder)
    {
        builder.ToTable("compra_nfe_impostos");
        builder.HasKey(x => x.CompraId);
        builder.Property(x => x.CompraId).HasColumnName("compra_id").ValueGeneratedNever();
        builder.Property(x => x.BaseIcms).HasColumnName("base_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIcms).HasColumnName("valor_icms").HasColumnType("numeric(18,2)");
        builder.Property(x => x.BaseIcmsSt).HasColumnName("base_icms_st").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIcmsSt).HasColumnName("valor_icms_st").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorTotalProdutos).HasColumnName("valor_total_produtos").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorFrete).HasColumnName("valor_frete").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorSeguro).HasColumnName("valor_seguro").HasColumnType("numeric(18,2)");
        builder.Property(x => x.Desconto).HasColumnName("desconto").HasColumnType("numeric(18,2)");
        builder.Property(x => x.OutrasDespesas).HasColumnName("outras_despesas").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorIpi).HasColumnName("valor_ipi").HasColumnType("numeric(18,2)");
        builder.Property(x => x.ValorTotalNota).HasColumnName("valor_total_nota").HasColumnType("numeric(18,2)");
    }
}

public class CompraNfeTransportadorMap : IEntityTypeConfiguration<CompraNfeTransportador>
{
    public void Configure(EntityTypeBuilder<CompraNfeTransportador> builder)
    {
        builder.ToTable("compra_nfe_transportadores");
        builder.HasKey(x => x.CompraId);
        builder.Property(x => x.CompraId).HasColumnName("compra_id").ValueGeneratedNever();
        builder.Property(x => x.NomeRazaoSocial).HasColumnName("nome_razao_social").HasMaxLength(300);
        builder.Property(x => x.FretePorConta).HasColumnName("frete_por_conta").HasMaxLength(80);
        builder.Property(x => x.CodigoAntt).HasColumnName("codigo_antt").HasMaxLength(40);
        builder.Property(x => x.Placa).HasColumnName("placa").HasMaxLength(20);
        builder.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
        builder.Property(x => x.CpfCnpj).HasColumnName("cpf_cnpj").HasMaxLength(20);
        builder.Property(x => x.Endereco).HasColumnName("endereco").HasMaxLength(500);
        builder.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(120);
        builder.Property(x => x.UfEndereco).HasColumnName("uf_endereco").HasMaxLength(2);
        builder.Property(x => x.InscricaoEstadual).HasColumnName("inscricao_estadual").HasMaxLength(30);
        builder.Property(x => x.QuantidadeVolumes).HasColumnName("quantidade_volumes").HasColumnType("numeric(18,4)");
        builder.Property(x => x.Especie).HasColumnName("especie").HasMaxLength(80);
        builder.Property(x => x.Marca).HasColumnName("marca").HasMaxLength(80);
        builder.Property(x => x.Numeracao).HasColumnName("numeracao").HasMaxLength(80);
        builder.Property(x => x.PesoBruto).HasColumnName("peso_bruto").HasColumnType("numeric(18,4)");
        builder.Property(x => x.PesoLiquido).HasColumnName("peso_liquido").HasColumnType("numeric(18,4)");
    }
}

public class CompraNfeAdicionaisMap : IEntityTypeConfiguration<CompraNfeAdicionais>
{
    public void Configure(EntityTypeBuilder<CompraNfeAdicionais> builder)
    {
        builder.ToTable("compra_nfe_adicionais");
        builder.HasKey(x => x.CompraId);
        builder.Property(x => x.CompraId).HasColumnName("compra_id").ValueGeneratedNever();
        builder.Property(x => x.InformacoesComplementares).HasColumnName("informacoes_complementares").HasMaxLength(4000);
        builder.Property(x => x.ReservadoAoFisco).HasColumnName("reservado_ao_fisco").HasMaxLength(2000);
        builder.Property(x => x.DataHoraImpressao).HasColumnName("data_hora_impressao");
    }
}
