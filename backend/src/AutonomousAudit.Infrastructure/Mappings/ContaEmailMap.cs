using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class ContaEmailMap : IEntityTypeConfiguration<ContaEmail>
{
    public void Configure(EntityTypeBuilder<ContaEmail> builder)
    {
        builder.ToTable("mail_accounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Remetente)
            .HasColumnName("remetente")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Senha)
            .HasColumnName("password")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Servidor)
            .HasColumnName("server")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.PortaSmtp)
            .HasColumnName("smtp_port")
            .IsRequired();

        builder.Property(x => x.PortaPop)
            .HasColumnName("pop_port");

        builder.Property(x => x.Protocolo)
            .HasColumnName("protocol")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ModoTls)
            .HasColumnName("tls_mode")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.Email).IsUnique();
    }
}
