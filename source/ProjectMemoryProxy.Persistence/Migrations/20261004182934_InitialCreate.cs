using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMemoryProxy.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectRoutings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemoryProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemoryProjectName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRoutings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContextBindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoutingProjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    BindingType = table.Column<string>(type: "TEXT", nullable: false),
                    BindingName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContextBindings_ProjectRoutings_RoutingProjectId",
                        column: x => x.RoutingProjectId,
                        principalTable: "ProjectRoutings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContextBindings_BindingType_BindingName",
                table: "ContextBindings",
                columns: new[] { "BindingType", "BindingName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContextBindings_RoutingProjectId",
                table: "ContextBindings",
                column: "RoutingProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRoutings_MemoryProjectId",
                table: "ProjectRoutings",
                column: "MemoryProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRoutings_MemoryProjectName",
                table: "ProjectRoutings",
                column: "MemoryProjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRoutings_UpdatedAt",
                table: "ProjectRoutings",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContextBindings");

            migrationBuilder.DropTable(
                name: "ProjectRoutings");
        }
    }
}
