using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlataformaCreditos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<string>(type: "TEXT", nullable: false),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    FechaEventoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Contenido = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_MessageId",
                table: "Notificaciones",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notificaciones");
        }
    }
}
