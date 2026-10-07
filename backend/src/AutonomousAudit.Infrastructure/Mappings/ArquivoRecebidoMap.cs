using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class ArquivoRecebidoMap : IEntityTypeConfiguration<ArquivoRecebido>
{
    public void Configure(EntityTypeBuilder<ArquivoRecebido> builder)
    {
        builder.ToTable("arquivos_recebidos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.NomeArquivo)
            .HasColumnName("nome_arquivo")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.TamanhoMb)
            .HasColumnName("tamanho_mb")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        builder.Property(x => x.DataHora)
            .HasColumnName("data_hora")
            .IsRequired();

        builder.Property(x => x.UsuarioRemetente)
            .HasColumnName("usuario_remetente")
            .HasMaxLength(320)
            .IsRequired();
    }
}
