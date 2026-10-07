using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class PerplexityPromptMap : IEntityTypeConfiguration<PerplexityPrompt>
{
    public void Configure(EntityTypeBuilder<PerplexityPrompt> builder)
    {
        builder.ToTable("perplexity_prompt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.Prompt).HasColumnName("prompt").HasColumnType("text").IsRequired();
        builder.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(120);
        builder.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100);
        builder.Property(x => x.CaminhoLocal).HasColumnName("caminho_local").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.MaxTentativas).HasColumnName("max_tentativas").IsRequired();
        builder.Property(x => x.UltimaMensagemErro).HasColumnName("ultima_mensagem_erro").HasMaxLength(1000);
        builder.Property(x => x.ModeloResposta).HasColumnName("modelo_resposta").HasMaxLength(120);
        builder.Property(x => x.TextoResposta).HasColumnName("texto_resposta").HasColumnType("text");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.Property(x => x.DataConclusao).HasColumnName("data_conclusao");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.UsuarioId);
    }
}

public class PerplexityArquivoMap : IEntityTypeConfiguration<PerplexityArquivo>
{
    public void Configure(EntityTypeBuilder<PerplexityArquivo> builder)
    {
        builder.ToTable("perplexity_arquivo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.ArquivoRecebidoId).HasColumnName("arquivo_recebido_id").IsRequired();
        builder.Property(x => x.DropboxDispatchId).HasColumnName("dropbox_dispatch_id").IsRequired();
        builder.Property(x => x.Prompt).HasColumnName("prompt").HasColumnType("text").IsRequired();
        builder.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(120);
        builder.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100);
        builder.Property(x => x.NomeOriginal).HasColumnName("nome_original").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CaminhoLocal).HasColumnName("caminho_local").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CaminhoDropbox).HasColumnName("caminho_dropbox").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DropboxInscrito).HasColumnName("dropbox_inscrito").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.MaxTentativas).HasColumnName("max_tentativas").IsRequired();
        builder.Property(x => x.UltimaMensagemErro).HasColumnName("ultima_mensagem_erro").HasMaxLength(1000);
        builder.Property(x => x.ModeloResposta).HasColumnName("modelo_resposta").HasMaxLength(120);
        builder.Property(x => x.TextoResposta).HasColumnName("texto_resposta").HasColumnType("text");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.Property(x => x.DataConclusao).HasColumnName("data_conclusao");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => x.ArquivoRecebidoId);
        builder.HasIndex(x => x.DropboxDispatchId);
    }
}

public sealed class UsuarioPerplexityUsoMensalMap : IEntityTypeConfiguration<UsuarioPerplexityUsoMensal>
{
    public void Configure(EntityTypeBuilder<UsuarioPerplexityUsoMensal> builder)
    {
        builder.ToTable("usuario_perplexity_uso_mensal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.Ano).HasColumnName("ano").IsRequired();
        builder.Property(x => x.Mes).HasColumnName("mes").IsRequired();
        builder.Property(x => x.Tokens).HasColumnName("tokens").IsRequired();
        builder.Property(x => x.CustoBrl).HasColumnName("custo_brl").HasPrecision(14, 6).IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.HasIndex(x => new { x.UsuarioId, x.Ano, x.Mes }).IsUnique();
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
