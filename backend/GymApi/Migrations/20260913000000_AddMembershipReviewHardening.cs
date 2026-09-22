using System;
using GymApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymApi.Migrations
{
    [DbContext(typeof(GymDbContext))]
    [Migration("20260913000000_AddMembershipReviewHardening")]
    public partial class AddMembershipReviewHardening : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Earlier local databases may already contain these review fields from the first Day 8 migration.
            migrationBuilder.Sql("ALTER TABLE \"Memberships\" ADD COLUMN IF NOT EXISTS \"PlanName\" text;");
            migrationBuilder.Sql("ALTER TABLE \"Memberships\" ADD COLUMN IF NOT EXISTS \"PlanPrice\" numeric;");
            migrationBuilder.Sql("ALTER TABLE \"Memberships\" ADD COLUMN IF NOT EXISTS \"PlanDurationInDays\" integer;");
            migrationBuilder.Sql("ALTER TABLE \"Memberships\" ADD COLUMN IF NOT EXISTS \"PlanBenefits\" text;");

            migrationBuilder.Sql("UPDATE \"Memberships\" SET \"Status\" = 'Approved' WHERE \"Status\" = 'Active';");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Memberships_UserId_Pending\" ON \"Memberships\" (\"UserId\") WHERE \"Status\" = 'Pending';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Memberships_UserId_Pending", table: "Memberships");
            migrationBuilder.DropColumn(name: "PlanName", table: "Memberships");
            migrationBuilder.DropColumn(name: "PlanPrice", table: "Memberships");
            migrationBuilder.DropColumn(name: "PlanDurationInDays", table: "Memberships");
            migrationBuilder.DropColumn(name: "PlanBenefits", table: "Memberships");

            migrationBuilder.Sql("UPDATE \"Memberships\" SET \"Status\" = 'Active' WHERE \"Status\" = 'Approved';");
        }
    }
}
