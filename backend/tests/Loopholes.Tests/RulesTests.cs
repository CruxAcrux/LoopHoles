using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Tests;

public class EnergyMathTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Regenerates_one_point_per_six_minutes()
    {
        var (energy, _) = EnergyMath.Regenerate(0, T0, T0.AddMinutes(18));
        Assert.Equal(3, energy);
    }

    [Fact]
    public void Full_refill_takes_sixty_minutes()
    {
        var (energy, _) = EnergyMath.Regenerate(0, T0, T0.AddMinutes(60));
        Assert.Equal(GameData.MaxEnergy, energy);
    }

    [Fact]
    public void Never_exceeds_the_cap_however_long_the_player_is_away()
    {
        var (energy, _) = EnergyMath.Regenerate(0, T0, T0.AddDays(30));
        Assert.Equal(GameData.MaxEnergy, energy);
    }

    [Fact]
    public void Partial_progress_toward_the_next_point_is_preserved()
    {
        // Five minutes buys nothing yet, but must not reset the clock either —
        // one more minute has to complete the point.
        var (first, stamp) = EnergyMath.Regenerate(0, T0, T0.AddMinutes(5));
        Assert.Equal(0, first);

        var (second, _) = EnergyMath.Regenerate(first, stamp, T0.AddMinutes(6));
        Assert.Equal(1, second);
    }

    [Fact]
    public void Sitting_at_full_does_not_bank_extra_points()
    {
        var (_, stamp) = EnergyMath.Regenerate(GameData.MaxEnergy, T0, T0.AddHours(10));

        // Dropping to 9 should still need a full six minutes, not resolve instantly.
        var (energy, _) = EnergyMath.Regenerate(GameData.MaxEnergy - 1, stamp, stamp.AddMinutes(5));
        Assert.Equal(GameData.MaxEnergy - 1, energy);
    }

    [Fact]
    public void Reports_when_the_pool_will_be_full()
    {
        Assert.Equal(T0.AddMinutes(60), EnergyMath.FullAt(0, T0));
        Assert.Null(EnergyMath.FullAt(GameData.MaxEnergy, T0));
    }
}

public class TrustMathTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, TrustTier.Untrustworthy)]
    [InlineData(29, TrustTier.Untrustworthy)]
    [InlineData(30, TrustTier.Unproven)]
    [InlineData(50, TrustTier.Unproven)]
    [InlineData(69, TrustTier.Unproven)]
    [InlineData(70, TrustTier.Trusted)]
    [InlineData(89, TrustTier.Trusted)]
    [InlineData(90, TrustTier.Elite)]
    [InlineData(100, TrustTier.Elite)]
    public void Tier_boundaries_match_the_brief(int score, TrustTier expected) =>
        Assert.Equal(expected, TrustMath.TierFor(score));

    [Fact]
    public void Idle_decay_pulls_a_high_score_down_toward_neutral()
    {
        var (score, _) = TrustMath.ApplyIdleDecay(80, T0, T0.AddDays(5));
        Assert.Equal(75, score);
    }

    [Fact]
    public void Idle_decay_pulls_a_low_score_up_toward_neutral()
    {
        var (score, _) = TrustMath.ApplyIdleDecay(20, T0, T0.AddDays(5));
        Assert.Equal(25, score);
    }

    [Fact]
    public void Idle_decay_stops_at_neutral_and_never_overshoots()
    {
        var (high, _) = TrustMath.ApplyIdleDecay(55, T0, T0.AddDays(100));
        var (low, _) = TrustMath.ApplyIdleDecay(45, T0, T0.AddDays(100));

        Assert.Equal(GameData.TrustNeutral, high);
        Assert.Equal(GameData.TrustNeutral, low);
    }

    [Fact]
    public void Untrustworthy_players_recover_faster_than_the_normal_rate()
    {
        Assert.Equal(GameData.TrustCleanStreakRecovery, TrustMath.HonestDealReward(10));
        Assert.Equal(GameData.TrustFairSplit, TrustMath.HonestDealReward(50));
        Assert.True(TrustMath.HonestDealReward(10) > TrustMath.HonestDealReward(50));
    }

    [Fact]
    public void Restitution_is_one_and_a_half_times_what_was_taken()
    {
        Assert.Equal(1500m, TrustMath.RestitutionOwed(1000m));
    }

    [Fact]
    public void Score_is_clamped_to_the_zero_to_hundred_range()
    {
        Assert.Equal(0, TrustMath.Clamp(-40));
        Assert.Equal(100, TrustMath.Clamp(140));
    }
}

public class TravelMathTests
{
    [Fact]
    public void Neutral_trust_leaves_the_base_risk_untouched()
    {
        Assert.Equal(0.08, TravelMath.AdjustedRisk(0.08, 50), 6);
    }

    [Fact]
    public void The_worked_example_from_the_spreadsheet_reproduces()
    {
        // Sheet "Travel Risk": Car at trust 72 → 6.24%.
        Assert.Equal(0.0624, TravelMath.AdjustedRisk(0.08, 72), 6);
        Assert.Equal(0.117, TravelMath.AdjustedRisk(0.15, 72), 6);
        Assert.Equal(0.0234, TravelMath.AdjustedRisk(0.03, 72), 6);
    }

    [Fact]
    public void Honesty_lowers_risk_and_dishonesty_raises_it()
    {
        var elite = TravelMath.AdjustedRisk(0.10, 100);
        var neutral = TravelMath.AdjustedRisk(0.10, 50);
        var crook = TravelMath.AdjustedRisk(0.10, 0);

        Assert.True(elite < neutral);
        Assert.True(crook > neutral);
        Assert.Equal(0.05, elite, 6);
        Assert.Equal(0.15, crook, 6);
    }

    [Fact]
    public void Risk_can_never_leave_the_zero_to_one_range()
    {
        Assert.InRange(TravelMath.AdjustedRisk(0.99, 0), 0, 1);
        Assert.InRange(TravelMath.AdjustedRisk(0.01, 100), 0, 1);
    }

    [Fact]
    public void A_licence_cuts_a_quarter_off_car_trips_only()
    {
        Assert.Equal(120, TravelMath.Duration(Transport.Car, false).TotalMinutes);
        Assert.Equal(90, TravelMath.Duration(Transport.Car, true).TotalMinutes);
        Assert.Equal(20, TravelMath.Duration(Transport.Jet, true).TotalMinutes);
        Assert.Equal(360, TravelMath.Duration(Transport.Freight, true).TotalMinutes);
    }
}

public class MarketMathTests
{
    private static GoodDefinition Furniture => GameData.FindGood("vintage_furniture")!;

    [Fact]
    public void Furniture_appreciates_three_percent_per_day()
    {
        var value = MarketMath.AppreciatedValue(Furniture, 60m, TimeSpan.FromDays(5));
        Assert.Equal(69.00m, value);
    }

    [Fact]
    public void Furniture_appreciation_caps_at_thirty_percent_on_day_ten()
    {
        var dayTen = MarketMath.AppreciatedValue(Furniture, 60m, TimeSpan.FromDays(10));
        var dayFifty = MarketMath.AppreciatedValue(Furniture, 60m, TimeSpan.FromDays(50));

        Assert.Equal(78.00m, dayTen);
        Assert.Equal(dayTen, dayFifty);
    }

    [Fact]
    public void Heavy_buying_pushes_a_price_up_and_heavy_selling_pushes_it_down()
    {
        var chips = GameData.FindGood("grey_chips")!;

        var bought = MarketMath.NextPrice(chips, chips.BasePrice, netVolume: 2000, noise: 0);
        var sold = MarketMath.NextPrice(chips, chips.BasePrice, netVolume: -2000, noise: 0);

        Assert.True(bought > chips.BasePrice);
        Assert.True(sold < chips.BasePrice);
    }

    [Fact]
    public void Prices_stay_inside_the_floor_and_ceiling_band_under_sustained_pressure()
    {
        var chips = GameData.FindGood("grey_chips")!;
        var price = chips.BasePrice;

        for (var i = 0; i < 5000; i++)
            price = MarketMath.NextPrice(chips, price, netVolume: 100_000, noise: 1);

        Assert.InRange(price,
            chips.BasePrice * (decimal)GameData.MarketFloorMultiplier,
            chips.BasePrice * (decimal)GameData.MarketCeilingMultiplier);
    }

    [Fact]
    public void An_idle_market_drifts_back_toward_the_base_price()
    {
        var chips = GameData.FindGood("grey_chips")!;
        var price = chips.BasePrice * 3;

        for (var i = 0; i < 200; i++)
            price = MarketMath.NextPrice(chips, price, netVolume: 0, noise: 0);

        Assert.Equal((double)chips.BasePrice, (double)price, 1);
    }

    [Fact]
    public void The_trading_fee_is_two_percent()
    {
        Assert.Equal(2.00m, MarketMath.Fee(100m));
    }
}

public class ScoreMathTests
{
    [Fact]
    public void Empire_score_is_net_worth_before_any_trust_modifier()
    {
        Assert.Equal(1000m, ScoreMath.EmpireScore(600m, 400m, 50));
    }

    [Fact]
    public void Trusted_players_score_higher_than_untrustworthy_ones_on_identical_net_worth()
    {
        var elite = ScoreMath.EmpireScore(1000m, 0m, 95);
        var trusted = ScoreMath.EmpireScore(1000m, 0m, 75);
        var neutral = ScoreMath.EmpireScore(1000m, 0m, 50);
        var crook = ScoreMath.EmpireScore(1000m, 0m, 10);

        Assert.True(elite > trusted);
        Assert.True(trusted > neutral);
        Assert.True(neutral > crook);
    }
}
