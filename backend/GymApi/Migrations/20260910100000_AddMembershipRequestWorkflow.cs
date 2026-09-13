using System;
using Microsoft.EntityFrameworkCore.Migrations;
using GymApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace GymApi.Migrations
{
    public partial class AddMembershipRequestWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Benefits",
                table: "MembershipPlans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Memberships",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Benefits", table: "MembershipPlans");
            migrationBuilder.DropColumn(name: "RequestedAt", table: "Memberships");
            migrationBuilder.DropColumn(name: "ReviewedAt", table: "Memberships");
            migrationBuilder.DropColumn(name: "ReviewNote", table: "Memberships");

            migrationBuilder.Sql("UPDATE \"Memberships\" SET \"StartDate\" = CURRENT_TIMESTAMP WHERE \"StartDate\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Memberships\" SET \"EndDate\" = CURRENT_TIMESTAMP WHERE \"EndDate\" IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
