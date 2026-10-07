using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class DropboxDispatchMap : IEntityTypeConfiguration<DropboxDispatch>
{
    public void Configure(EntityTypeBuilder<DropboxDispatch> builder)
    {
        builder.ToTable("dropbox_dispatch");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ArquivoRecebidoId).HasColumnName("arquivo_recebido_id").IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NomeOriginal).HasColumnName("nome_original").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CaminhoLocal).HasColumnName("caminho_local").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CaminhoDropbox).HasColumnName("caminho_dropbox").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DropboxId).HasColumnName("dropbox_id").HasMaxLength(200);
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.MaxTentativas).HasColumnName("max_tentativas").IsRequired();
        builder.Property(x => x.UltimaMensagemErro).HasColumnName("ultima_mensagem_erro").HasMaxLength(1000);
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.Property(x => x.DataConclusao).HasColumnName("data_conclusao");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ArquivoRecebidoId);

        builder.HasOne(x => x.Arquivo)
            .WithMany()
            .HasForeignKey(x => x.ArquivoRecebidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
