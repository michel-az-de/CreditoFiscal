using CreditoFiscal.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace CreditoFiscal.Infraestrutura.Persistencia;

public sealed class CreditoFiscalDbContext : DbContext
{
    public CreditoFiscalDbContext(DbContextOptions<CreditoFiscalDbContext> options)
        : base(options)
    {
    }

    public DbSet<Credito> Creditos
    {
        get { return Set<Credito>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Credito>(b =>
        {
            b.ToTable("credito");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.NumeroCredito).HasColumnName("numero_credito").HasMaxLength(50).IsRequired();
            b.Property(x => x.NumeroNfse).HasColumnName("numero_nfse").HasMaxLength(50).IsRequired();

            // data fiscal sem hora/fuso -> DATE
            b.Property(x => x.DataConstituicao).HasColumnName("data_constituicao").HasColumnType("date");

            // moeda (15,2), aliquota (5,2)
            b.Property(x => x.ValorIssqn).HasColumnName("valor_issqn").HasPrecision(15, 2);
            b.Property(x => x.ValorFaturado).HasColumnName("valor_faturado").HasPrecision(15, 2);
            b.Property(x => x.ValorDeducao).HasColumnName("valor_deducao").HasPrecision(15, 2);
            b.Property(x => x.BaseCalculo).HasColumnName("base_calculo").HasPrecision(15, 2);
            b.Property(x => x.Aliquota).HasColumnName("aliquota").HasPrecision(5, 2);

            b.Property(x => x.TipoCredito).HasColumnName("tipo_credito").HasMaxLength(50).IsRequired();

            // enum no dominio, boolean no banco
            b.Property(x => x.SimplesNacional)
                .HasColumnName("simples_nacional")
                .HasConversion(
                    enumValor => enumValor == SimplesNacional.Optante,
                    boolValor => boolValor ? SimplesNacional.Optante : SimplesNacional.NaoOptante);

            // unique: mesmo numero_credito 2x seria duplicidade fiscal
            b.HasIndex(x => x.NumeroCredito).IsUnique().HasDatabaseName("ix_credito_numero_credito");
            b.HasIndex(x => x.NumeroNfse).HasDatabaseName("ix_credito_numero_nfse");
        });
    }
}
