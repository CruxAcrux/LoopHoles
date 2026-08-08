using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopholes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketGoods",
                columns: table => new
                {
                    GoodId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CurrentPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    PriceOneHourAgo = table.Column<long>(type: "INTEGER", nullable: false),
                    PriceOneHourAgoAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    NetVolumeSinceTick = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveEvent = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EventStartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    EventEndsAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CrashPending = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketGoods", x => x.GoodId);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Cash = table.Column<long>(type: "INTEGER", nullable: false),
                    Favor = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergySnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyUpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    TrustScore = table.Column<int>(type: "INTEGER", nullable: false),
                    TrustDecayedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LicenseStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LicenseStudyCompletesAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LicenseRetryAvailableAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LicenseObtainedByCheating = table.Column<bool>(type: "INTEGER", nullable: false),
                    FreezeTier = table.Column<int>(type: "INTEGER", nullable: false),
                    FreezeEndsAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FreezeNextPaperworkAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CheatingOffenses = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryCap = table.Column<int>(type: "INTEGER", nullable: false),
                    HasPaperworkDiscount = table.Column<bool>(type: "INTEGER", nullable: false),
                    TravelTransport = table.Column<int>(type: "INTEGER", nullable: true),
                    TravelDestination = table.Column<string>(type: "TEXT", nullable: true),
                    TravelArrivesAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LifetimeCashEarned = table.Column<long>(type: "INTEGER", nullable: false),
                    JobsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesFrozen = table.Column<int>(type: "INTEGER", nullable: false),
                    BestEmpireScore = table.Column<long>(type: "INTEGER", nullable: false),
                    BestSeasonRank = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndsAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Closed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Number);
                });

            migrationBuilder.CreateTable(
                name: "ActiveJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletesAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RolledPayout = table.Column<long>(type: "INTEGER", nullable: false),
                    RiskTriggered = table.Column<bool>(type: "INTEGER", nullable: false),
                    CollectedSoFar = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveJobs_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryStacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoodId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    AcquiredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryStacks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerCosmetics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CosmeticId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Equipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcquiredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCosmetics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerCosmetics_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmpireScore = table.Column<long>(type: "INTEGER", nullable: false),
                    Cash = table.Column<long>(type: "INTEGER", nullable: false),
                    TrustScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ReachedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonScores_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Delta = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustEvents_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveJobs_PlayerId_CompletesAt",
                table: "ActiveJobs",
                columns: new[] { "PlayerId", "CompletesAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStacks_PlayerId_GoodId",
                table: "InventoryStacks",
                columns: new[] { "PlayerId", "GoodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCosmetics_PlayerId_CosmeticId",
                table: "PlayerCosmetics",
                columns: new[] { "PlayerId", "CosmeticId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_DeviceId",
                table: "Players",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_TokenHash",
                table: "Players",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonScores_PlayerId",
                table: "SeasonScores",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonScores_SeasonNumber_EmpireScore",
                table: "SeasonScores",
                columns: new[] { "SeasonNumber", "EmpireScore" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonScores_SeasonNumber_PlayerId",
                table: "SeasonScores",
                columns: new[] { "SeasonNumber", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrustEvents_PlayerId_At",
                table: "TrustEvents",
                columns: new[] { "PlayerId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveJobs");

            migrationBuilder.DropTable(
                name: "InventoryStacks");

            migrationBuilder.DropTable(
                name: "MarketGoods");

            migrationBuilder.DropTable(
                name: "PlayerCosmetics");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "SeasonScores");

            migrationBuilder.DropTable(
                name: "TrustEvents");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
