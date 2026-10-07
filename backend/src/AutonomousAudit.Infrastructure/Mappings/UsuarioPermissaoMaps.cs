using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousAudit.Infrastructure.Mappings;

public class UsuarioMap : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.EmailPrincipal).HasColumnName("email_principal").HasMaxLength(320).IsRequired();
        builder.Property(x => x.EmailSecundario).HasColumnName("email_secundario").HasMaxLength(320);
        builder.Property(x => x.TelefonePrincipal).HasColumnName("telefone_principal").HasMaxLength(30);
        builder.Property(x => x.TelefoneSecundario).HasColumnName("telefone_secundario").HasMaxLength(30);
        builder.Property(x => x.TipoDocto).HasColumnName("tipo_docto").HasMaxLength(50).IsRequired();
        builder.Property(x => x.NDocto).HasColumnName("n_docto").HasMaxLength(50).IsRequired();
        builder.Property(x => x.DataEmissao).HasColumnName("data_emissao");
        builder.Property(x => x.Orgao).HasColumnName("orgao").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Cidade).HasColumnName("cidade").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Endereco).HasColumnName("endereco").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Cep).HasColumnName("cep").HasMaxLength(9).IsRequired();
        builder.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SmsAuth).HasColumnName("sms_auth").IsRequired();
        builder.Property(x => x.EmailAuth).HasColumnName("email_auth").IsRequired();
        builder.Property(x => x.SmsAuthCode).HasColumnName("sms_auth_code").HasMaxLength(6);
        builder.Property(x => x.EmailAuthCode).HasColumnName("email_auth_code").HasMaxLength(6);
        builder.Property(x => x.EmailPrincipalVerificado).HasColumnName("email_principal_verificado").IsRequired();
        builder.Property(x => x.EmailSecundarioVerificado).HasColumnName("email_secundario_verificado").IsRequired();
        builder.Property(x => x.TelefonePrincipalVerificado).HasColumnName("telefone_principal_verificado").IsRequired();
        builder.Property(x => x.TelefoneSecundarioVerificado).HasColumnName("telefone_secundario_verificado").IsRequired();
        builder.Property(x => x.EmailVerificacaoCodigo).HasColumnName("email_verificacao_codigo").HasMaxLength(6);
        builder.Property(x => x.EmailVerificacaoCanal).HasColumnName("email_verificacao_canal").HasMaxLength(20);
        builder.Property(x => x.EmailVerificacaoExpira).HasColumnName("email_verificacao_expira");
        builder.Property(x => x.SmsVerificacaoCodigo).HasColumnName("sms_verificacao_codigo").HasMaxLength(6);
        builder.Property(x => x.SmsVerificacaoCanal).HasColumnName("sms_verificacao_canal").HasMaxLength(20);
        builder.Property(x => x.SmsVerificacaoExpira).HasColumnName("sms_verificacao_expira");
        builder.Property(x => x.ApiTokenJti).HasColumnName("api_token_jti").HasMaxLength(64);
        builder.Property(x => x.ApiTokenExpira).HasColumnName("api_token_expira");
        builder.Property(x => x.PerplexityTokensTotal).HasColumnName("perplexity_tokens_total").IsRequired();
        builder.Property(x => x.PerplexityCustoTotalBrl)
            .HasColumnName("perplexity_custo_total_brl")
            .HasPrecision(14, 6)
            .IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").HasMaxLength(128).IsRequired();
        builder.Property(x => x.SenhaHash).HasColumnName("senha_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.GoogleSub).HasColumnName("google_sub").HasMaxLength(128);
        builder.Property(x => x.GooglePicture).HasColumnName("google_picture").HasMaxLength(2000);
        builder.Property(x => x.SenhaResetCodigo).HasColumnName("senha_reset_codigo").HasMaxLength(6);
        builder.Property(x => x.SenhaResetExpira).HasColumnName("senha_reset_expira");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao").IsRequired();

        builder.HasIndex(x => x.EmailPrincipal).IsUnique();
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.GoogleSub).IsUnique().HasFilter("google_sub IS NOT NULL");

        builder.HasMany(x => x.Permissoes)
            .WithOne(x => x.Usuario)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Permissoes)
            .HasField("_permissoes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ModuloMap : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("modulos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.Codigo).IsUnique();

        builder.HasMany(x => x.Recursos)
            .WithOne(x => x.Modulo)
            .HasForeignKey(x => x.ModuloId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Recursos)
            .HasField("_recursos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class RecursoMap : IEntityTypeConfiguration<Recurso>
{
    public void Configure(EntityTypeBuilder<Recurso> builder)
    {
        builder.ToTable("recursos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ModuloId).HasColumnName("modulo_id").IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Chave).HasColumnName("chave").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.MetodoHttp).HasColumnName("metodo_http").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Rota).HasColumnName("rota").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Chave).IsUnique();
        builder.HasIndex(x => new { x.ModuloId, x.Codigo }).IsUnique();
    }
}

public class UsuarioPermissaoMap : IEntityTypeConfiguration<UsuarioPermissao>
{
    public void Configure(EntityTypeBuilder<UsuarioPermissao> builder)
    {
        builder.ToTable("usuario_permissoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.ModuloId).HasColumnName("modulo_id").IsRequired();
        builder.Property(x => x.RecursoId).HasColumnName("recurso_id").IsRequired();
        builder.HasIndex(x => new { x.UsuarioId, x.RecursoId }).IsUnique();

        builder.HasOne(x => x.Modulo)
            .WithMany()
            .HasForeignKey(x => x.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Recurso)
            .WithMany()
            .HasForeignKey(x => x.RecursoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdministradorSistemaMap : IEntityTypeConfiguration<AdministradorSistema>
{
    public void Configure(EntityTypeBuilder<AdministradorSistema> builder)
    {
        builder.ToTable("adm_sys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id_admin").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("id_usuario").IsRequired();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao").IsRequired();
        builder.HasIndex(x => x.UsuarioId).IsUnique();

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
