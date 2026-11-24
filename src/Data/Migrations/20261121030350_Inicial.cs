using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analistas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analistas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reclamaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BeneficiaryDocument = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BeneficiaryEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BeneficiaryPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InsuredName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InsuredDocument = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FreeText = table.Column<string>(type: "text", nullable: false),
                    TipoInferido = table.Column<int>(type: "integer", nullable: false),
                    Confianza = table.Column<double>(type: "double precision", nullable: true),
                    InferenciaRaw = table.Column<string>(type: "text", nullable: true),
                    TipoConfirmado = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletadoEl = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaLimite = table.Column<DateOnly>(type: "date", nullable: true),
                    PrimerAvisoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclamaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decisiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalistaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalistaNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Accion = table.Column<int>(type: "integer", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decisiones_reclamaciones_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "reclamaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreArchivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Ruta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    TipoDetectado = table.Column<int>(type: "integer", nullable: true),
                    Validacion = table.Column<int>(type: "integer", nullable: false),
                    ExtraidoJson = table.Column<string>(type: "text", nullable: true),
                    SubidoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documentos_reclamaciones_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "reclamaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eventos_reclamaciones_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "reclamaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inferencias_ia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Proveedor = table.Column<int>(type: "integer", nullable: false),
                    Modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SolicitudJson = table.Column<string>(type: "text", nullable: false),
                    RespuestaRaw = table.Column<string>(type: "text", nullable: true),
                    Exito = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    LatenciaMs = table.Column<int>(type: "integer", nullable: false),
                    Confianza = table.Column<double>(type: "double precision", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inferencias_ia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inferencias_ia_reclamaciones_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "reclamaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnviadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    UltimoError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outbox_reclamaciones_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "reclamaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analistas_Email",
                table: "analistas",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decisiones_ClaimId",
                table: "decisiones",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_ClaimId_ContentHash",
                table: "documentos",
                columns: new[] { "ClaimId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eventos_ClaimId_OcurridoEn",
                table: "eventos",
                columns: new[] { "ClaimId", "OcurridoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_inferencias_ia_ClaimId_CreadoEn",
                table: "inferencias_ia",
                columns: new[] { "ClaimId", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_ClaimId",
                table: "outbox",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_Estado_CreadoEn",
                table: "outbox",
                columns: new[] { "Estado", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_reclamaciones_ExternalEventId",
                table: "reclamaciones",
                column: "ExternalEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reclamaciones_PublicToken",
                table: "reclamaciones",
                column: "PublicToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analistas");

            migrationBuilder.DropTable(
                name: "decisiones");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "eventos");

            migrationBuilder.DropTable(
                name: "inferencias_ia");

            migrationBuilder.DropTable(
                name: "outbox");

            migrationBuilder.DropTable(
                name: "reclamaciones");
        }
    }
}
