using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GimnasioFITLAB.Migrations
{
    /// <inheritdoc />
    public partial class BajaLogicaMiembroHU05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Miembros",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpleadoBajaId",
                table: "Miembros",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaBaja",
                table: "Miembros",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Miembros");

            migrationBuilder.DropColumn(
                name: "EmpleadoBajaId",
                table: "Miembros");

            migrationBuilder.DropColumn(
                name: "FechaBaja",
                table: "Miembros");
        }
    }
}
