﻿#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace ProjectManagement.CompanyAPI.Data.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Company",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>("character varying(255)", maxLength: 255, nullable: false),
                CreatedBy = table.Column<string>("text", nullable: false),
                CreatedOn = table.Column<DateTime>("timestamp with time zone", nullable: false),
                ModifiedBy = table.Column<string>("text", nullable: false),
                ModifiedOn = table.Column<DateTime>("timestamp with time zone", nullable: false),
            },
            constraints: table => { table.PrimaryKey("PK_Company", x => x.Id); });

        migrationBuilder.CreateTable(
            "Tag",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                CreatedBy = table.Column<int>("integer", nullable: false),
                CreatedOn = table.Column<DateTime>("timestamp with time zone", nullable: false),
                ModifiedBy = table.Column<int>("integer", nullable: false),
                ModifiedOn = table.Column<DateTime>("timestamp with time zone", nullable: false),
            },
            constraints: table => { table.PrimaryKey("PK_Tag", x => x.Id); });

        migrationBuilder.CreateTable(
            "CompanyTag",
            table => new
            {
                CompaniesId = table.Column<int>("integer", nullable: false),
                TagsId = table.Column<int>("integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CompanyTag", x => new { x.CompaniesId, x.TagsId });
                table.ForeignKey(
                    "FK_CompanyTag_Company_CompaniesId",
                    x => x.CompaniesId,
                    "Company",
                    "Id",
                    onDelete: ReferentialAction.Cascade);

                table.ForeignKey(
                    "FK_CompanyTag_Tag_TagsId",
                    x => x.TagsId,
                    "Tag",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_CompanyTag_TagsId",
            "CompanyTag",
            "TagsId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "CompanyTag");

        migrationBuilder.DropTable(
            "Company");

        migrationBuilder.DropTable(
            "Tag");
    }
}