using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Tests;

/// <summary>
/// Guards the transcription of loopholes_economy_model.xlsx into GameData. If a
/// balance pass changes a number in the spreadsheet, exactly one of these should
/// fail — which is the point: the two are supposed to stay in step.
/// </summary>
public class BalanceTests
{
    [Fact]
    public void Global_settings_match_the_spreadsheet()
    {
        Assert.Equal(10, GameData.MaxEnergy);
        Assert.Equal(6, GameData.EnergyRegenMinutes);
        Assert.Equal(60, GameData.MaxEnergy * GameData.EnergyRegenMinutes);
    }

    [Theory]
    // Job id, duration (min), energy, cash cost, payout min, payout max
    [InlineData("flip_crate", 0.5, 1, 0, 5, 8)]
    [InlineData("courier_run", 3, 1, 0, 10, 15)]
    [InlineData("sidewalk_sale", 15, 2, 0, 25, 35)]
    [InlineData("cook_books", 10, 2, 0, 30, 40)]
    [InlineData("ghost_invoice", 30, 2, 50, 90, 120)]
    [InlineData("overnight_storage", 480, 3, 0, 150, 200)]
    [InlineData("sneaker_line", 5, 1, 0, 15, 20)]
    [InlineData("follower_farm", 8, 1, 0, 18, 25)]
    [InlineData("pawn_goods", 20, 2, 0, 35, 45)]
    [InlineData("exam_key_delivery", 25, 2, 20, 55, 70)]
    [InlineData("furniture_pickup", 45, 2, 0, 10, 10)]
    [InlineData("late_shift_cleanup", 90, 3, 0, 110, 140)]
    [InlineData("import_run", 120, 4, 200, 350, 450)]
    [InlineData("warehouse_clearance", 240, 5, 400, 700, 900)]
    [InlineData("popup_storefront", 360, 5, 300, 600, 750)]
    [InlineData("streaming_wholesale", 180, 4, 250, 450, 550)]
    [InlineData("chip_smuggling", 150, 4, 300, 400, 700)]
    [InlineData("crackdown_dodge", 180, 4, 250, 500, 650)]
    [InlineData("vintage_flip", 300, 3, 50, 100, 100)]
    [InlineData("paperwork_laundering", 240, 3, 200, 150, 150)]
    public void Job_numbers_match_the_spreadsheet(string id, double minutes, int energy, decimal cost, decimal min, decimal max)
    {
        var job = GameData.FindJob(id);
        Assert.NotNull(job);
        Assert.Equal(minutes, job.DurationMinutes);
        Assert.Equal(energy, job.EnergyCost);
        Assert.Equal(cost, job.CashCost);
        Assert.Equal(min, job.PayoutMin);
        Assert.Equal(max, job.PayoutMax);
    }

    [Fact]
    public void Every_tier_one_and_tier_two_job_from_the_brief_is_present()
    {
        Assert.Equal(12, GameData.Jobs.Count(j => j.Tier == JobTier.Hustle));
        Assert.Equal(8, GameData.Jobs.Count(j => j.Tier == JobTier.Operation));
    }

    [Fact]
    public void Job_ids_are_unique()
    {
        Assert.Equal(GameData.Jobs.Count, GameData.Jobs.Select(j => j.Id).Distinct().Count());
    }

    [Fact]
    public void Only_the_two_designated_operations_carry_a_risk_roll()
    {
        var risky = GameData.Jobs.Where(j => j.RiskChance > 0).Select(j => j.Id).OrderBy(x => x).ToArray();
        Assert.Equal(["crackdown_dodge", "warehouse_clearance"], risky);

        Assert.Equal(0.08, GameData.FindJob("warehouse_clearance")!.RiskChance);
        Assert.Equal(0.20, GameData.FindJob("warehouse_clearance")!.RiskMagnitude);
        Assert.Equal(0.10, GameData.FindJob("crackdown_dodge")!.RiskChance);
        Assert.Equal(0.30, GameData.FindJob("crackdown_dodge")!.RiskMagnitude);
    }

    [Fact]
    public void No_tier_one_hustle_ever_costs_the_player_more_than_it_pays()
    {
        // The opening tier teaches the loop; it must never be a trap.
        foreach (var job in GameData.Jobs.Where(j => j.Tier == JobTier.Hustle))
            Assert.True(job.PayoutMin >= job.CashCost, $"{job.Name} can lose money at its floor payout.");
    }

    [Theory]
    [InlineData("streaming_codes", 10)]
    [InlineData("grey_chips", 50)]
    [InlineData("knockoff_sneakers", 80)]
    [InlineData("energy_drinks", 15)]
    [InlineData("exam_keys", 40)]
    [InlineData("influencer_followers", 5)]
    [InlineData("vintage_furniture", 60)]
    [InlineData("shell_paperwork", 25)]
    public void Market_base_prices_match_the_spreadsheet(string id, decimal basePrice)
    {
        var good = GameData.FindGood(id);
        Assert.NotNull(good);
        Assert.Equal(basePrice, good.BasePrice);
    }

    [Fact]
    public void The_market_ships_with_eight_goods_each_with_a_distinct_personality()
    {
        Assert.Equal(8, GameData.Goods.Count);
        Assert.Equal(8, GameData.Goods.Select(g => g.Id).Distinct().Count());

        // Seven distinct behaviours across eight goods: the two low-volatility
        // utility goods deliberately share the Steady personality.
        Assert.Equal(7, GameData.Goods.Select(g => g.Behavior).Distinct().Count());
    }

    [Fact]
    public void Licence_numbers_match_the_spreadsheet()
    {
        Assert.Equal(500m, GameData.LicenseCost);
        Assert.Equal(3, GameData.LicenseStudyHours);
        Assert.Equal(0.95, GameData.LicenseLegitPassRate);
        Assert.Equal(0.35, GameData.LicenseCheatCatchRate);
        Assert.Equal(300m, GameData.LicenseCaughtFine);
        Assert.Equal(48, GameData.LicenseCaughtCooldownHours);
        Assert.Equal(0.25, GameData.LicenseCarTimeReduction);
    }

    [Fact]
    public void Freeze_durations_costs_and_penalties_match_the_spreadsheet()
    {
        Assert.Equal(TimeSpan.FromHours(4), GameData.MinorFreezeDuration);
        Assert.Equal(TimeSpan.FromHours(24), GameData.MajorFreezeDuration);
        Assert.Equal(-10, GameData.TrustMinorFreezePenalty);
        Assert.Equal(-25, GameData.TrustMajorFreezePenalty);

        Assert.Equal(400m, GameData.MinorLawyerCost);
        Assert.Equal(2500m, GameData.MajorLawyerCost);
        Assert.Equal(1200m, GameData.MajorLawyerPartialCost);

        Assert.Equal(TimeSpan.FromMinutes(20), GameData.MinorPaperworkReduction);
        Assert.Equal(TimeSpan.FromMinutes(30), GameData.MinorPaperworkCooldown);
        Assert.Equal(TimeSpan.FromMinutes(45), GameData.MajorPaperworkReduction);
        Assert.Equal(TimeSpan.FromHours(1), GameData.MajorPaperworkCooldown);
    }

    [Fact]
    public void A_season_is_four_weeks()
    {
        Assert.Equal(TimeSpan.FromDays(28), GameData.SeasonLength);
    }

    [Fact]
    public void Cosmetics_are_priced_in_favor_only_and_include_a_free_starter()
    {
        Assert.Contains(GameData.Cosmetics, c => c.IsStarter && c.FavorCost == 0);
        Assert.InRange(GameData.Cosmetics.Count, 4, 6);
    }
}
