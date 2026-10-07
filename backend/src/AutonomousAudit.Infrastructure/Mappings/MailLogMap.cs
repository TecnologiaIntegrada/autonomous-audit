using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class MailLogMap : IEntityTypeConfiguration<MailLog>
{
    public void Configure(EntityTypeBuilder<MailLog> builder)
    {
        builder.ToTable("mail_log");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ContaId).HasColumnName("conta_id").IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.UsuarioToken).HasColumnName("usuario_token").HasMaxLength(64).IsRequired();
        builder.Property(x => x.De).HasColumnName("de").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Para).HasColumnName("para").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Assunto).HasColumnName("assunto").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NomeAnexo).HasColumnName("nome_anexo").HasMaxLength(500);
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.MaxTentativas).HasColumnName("max_tentativas").IsRequired();
        builder.Property(x => x.RespostaServidor).HasColumnName("resposta_servidor").HasMaxLength(2000);
        builder.Property(x => x.UltimaMensagemErro).HasColumnName("ultima_mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();
        builder.Property(x => x.DataEnvio).HasColumnName("data_envio");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ContaId);
        builder.HasIndex(x => x.UsuarioId);
    }
}
