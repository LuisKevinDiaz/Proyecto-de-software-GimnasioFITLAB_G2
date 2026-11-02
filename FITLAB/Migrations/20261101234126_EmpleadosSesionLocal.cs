using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GimnasioFITLAB.Migrations
{
    /// <inheritdoc />
    public partial class EmpleadosSesionLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmpleadosCuentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Usuario = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UsuarioNormalizado = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    PuedeActualizarContacto = table.Column<bool>(type: "bit", nullable: false),
                    PuedeDarDeBaja = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpleadosCuentas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SesionesEmpleados",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    ExpiraUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesEmpleados", x => x.TokenHash);
                    table.ForeignKey(
                        name: "FK_SesionesEmpleados_EmpleadosCuentas_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "EmpleadosCuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmpleadosCuentas_UsuarioNormalizado",
                table: "EmpleadosCuentas",
                column: "UsuarioNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesEmpleados_EmpleadoId",
                table: "SesionesEmpleados",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesEmpleados_ExpiraUtc",
                table: "SesionesEmpleados",
                column: "ExpiraUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionesEmpleados");

            migrationBuilder.DropTable(
                name: "EmpleadosCuentas");
        }
    }
}
