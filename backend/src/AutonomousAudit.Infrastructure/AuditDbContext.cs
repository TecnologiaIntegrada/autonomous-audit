using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    public DbSet<ArquivoRecebido> ArquivosRecebidos => Set<ArquivoRecebido>();
    public DbSet<DropboxDispatch> DropboxDispatches => Set<DropboxDispatch>();
    public DbSet<PerplexityPrompt> PerplexityPrompts => Set<PerplexityPrompt>();
    public DbSet<PerplexityArquivo> PerplexityArquivos => Set<PerplexityArquivo>();
    public DbSet<ContaEmail> ContasEmail => Set<ContaEmail>();
    public DbSet<MailLog> MailLogs => Set<MailLog>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPerplexityUsoMensal> UsuarioPerplexityUsosMensais => Set<UsuarioPerplexityUsoMensal>();
    public DbSet<AdministradorSistema> AdministradoresSistema => Set<AdministradorSistema>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<Recurso> Recursos => Set<Recurso>();
    public DbSet<UsuarioPermissao> UsuarioPermissoes => Set<UsuarioPermissao>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ProdutoNome> ProdutoNomes => Set<ProdutoNome>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraNfeDestinatario> CompraNfeDestinatarios => Set<CompraNfeDestinatario>();
    public DbSet<CompraNfeImposto> CompraNfeImpostos => Set<CompraNfeImposto>();
    public DbSet<CompraNfeTransportador> CompraNfeTransportadores => Set<CompraNfeTransportador>();
    public DbSet<CompraNfeAdicionais> CompraNfeAdicionais => Set<CompraNfeAdicionais>();
    public DbSet<CompraItem> CompraItens => Set<CompraItem>();
    public DbSet<CompraAnexo> CompraAnexos => Set<CompraAnexo>();
    public DbSet<CompraDocumento> CompraDocumentos => Set<CompraDocumento>();
    public DbSet<CapturaSessao> CapturaSessoes => Set<CapturaSessao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
