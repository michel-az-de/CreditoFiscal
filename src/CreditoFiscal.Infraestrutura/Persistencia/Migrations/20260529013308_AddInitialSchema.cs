using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreditoFiscal.Infraestrutura.Persistencia.Migrations
{
    public partial class AddInitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credito",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_credito = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    numero_nfse = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_constituicao = table.Column<DateTime>(type: "date", nullable: false),
                    valor_issqn = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    tipo_credito = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    simples_nacional = table.Column<bool>(type: "boolean", nullable: false),
                    aliquota = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    valor_faturado = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    valor_deducao = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    base_calculo = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credito", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credito_numero_credito",
                table: "credito",
                column: "numero_credito",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credito_numero_nfse",
                table: "credito",
                column: "numero_nfse");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credito");
        }
    }
}
