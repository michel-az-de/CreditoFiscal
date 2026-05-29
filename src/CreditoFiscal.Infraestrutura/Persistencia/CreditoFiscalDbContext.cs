using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace CreditoFiscal.Infraestrutura.Persistencia;

public sealed class CreditoFiscalDbContext : DbContext
{
    public CreditoFiscalDbContext(DbContextOptions<CreditoFiscalDbContext> options)
        : base(options)
    {
    }

    public DbSet<Credito> Creditos => Set<Credito>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Credito>(b =>
        {
            b.ToTable("credito");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.NumeroCredito).HasColumnName("numero_credito").IsRequired();
            b.Property(x => x.NumeroNfse).HasColumnName("numero_nfse").IsRequired();

            // C0: timestamp without time zone evita InvalidCastException do Npgsql 6 ao escrever
            // DateTime com Kind=Unspecified em coluna timezone-aware.
            b.Property(x => x.DataConstituicao)
                .HasColumnName("data_constituicao")
                .HasColumnType("timestamp without time zone");

            // C6: precisao explicita evita warning de runtime do EF Core 6 e
            // garante semantica fiscal (numeric com escala definida no Postgres).
            b.Property(x => x.ValorIssqn).HasColumnName("valor_issqn").HasPrecision(18, 2);
            b.Property(x => x.ValorFaturado).HasColumnName("valor_faturado").HasPrecision(18, 2);
            b.Property(x => x.ValorDeducao).HasColumnName("valor_deducao").HasPrecision(18, 2);
            b.Property(x => x.BaseCalculo).HasColumnName("base_calculo").HasPrecision(18, 2);
            b.Property(x => x.Aliquota).HasColumnName("aliquota").HasPrecision(5, 4);

            b.Property(x => x.TipoCredito).HasColumnName("tipo_credito").IsRequired();
            b.Property(x => x.SimplesNacional).HasColumnName("simples_nacional");

            // Indice unico em numero_credito: gate de idempotencia no banco.
            b.HasIndex(x => x.NumeroCredito).IsUnique().HasDatabaseName("ix_credito_numero_credito");
            b.HasIndex(x => x.NumeroNfse).HasDatabaseName("ix_credito_numero_nfse");
        });
    }
}
