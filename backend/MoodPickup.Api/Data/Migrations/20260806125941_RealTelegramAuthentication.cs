using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoodPickup.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RealTelegramAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CodeHash",
                table: "LoginChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "ClientStatusSecretHash",
                table: "LoginChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OtpSentAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "LoginChallenges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramContactVerifiedAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramDeliveryFailedAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TelegramDeliveryFailureCount",
                table: "LoginChallenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TelegramLinkAttemptCount",
                table: "LoginChallenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramLinkExpiresAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLinkTokenHash",
                table: "LoginChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramLinkUsedAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramLinkedAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramStartedAt",
                table: "LoginChallenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TelegramUserId",
                table: "LoginChallenges",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "LoginChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TelegramProcessedUpdates",
                columns: table => new
                {
                    UpdateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramProcessedUpdates", x => x.UpdateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginChallenges_ClientStatusSecretHash",
                table: "LoginChallenges",
                column: "ClientStatusSecretHash",
                unique: true,
                filter: "\"ClientStatusSecretHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginChallenges_TelegramLinkTokenHash",
                table: "LoginChallenges",
                column: "TelegramLinkTokenHash",
                unique: true,
                filter: "\"TelegramLinkTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginChallenges_TelegramUserId_TelegramStartedAt",
                table: "LoginChallenges",
                columns: new[] { "TelegramUserId", "TelegramStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramProcessedUpdates_ProcessedAt",
                table: "TelegramProcessedUpdates",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramProcessedUpdates");

            migrationBuilder.DropIndex(
                name: "IX_LoginChallenges_ClientStatusSecretHash",
                table: "LoginChallenges");

            migrationBuilder.DropIndex(
                name: "IX_LoginChallenges_TelegramLinkTokenHash",
                table: "LoginChallenges");

            migrationBuilder.DropIndex(
                name: "IX_LoginChallenges_TelegramUserId_TelegramStartedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "ClientStatusSecretHash",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "OtpSentAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramContactVerifiedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramDeliveryFailedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramDeliveryFailureCount",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramLinkAttemptCount",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramLinkExpiresAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramLinkTokenHash",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramLinkUsedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramLinkedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramStartedAt",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramUserId",
                table: "LoginChallenges");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "LoginChallenges");

            migrationBuilder.AlterColumn<string>(
                name: "CodeHash",
                table: "LoginChallenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
