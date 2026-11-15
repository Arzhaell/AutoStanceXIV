using AutoStanceXIV;

namespace AutoStanceXIV.Tests;

/// <summary>Décision de la synchronisation automatique dans les ALÉA.</summary>
public class FateSyncRulesTests
{
    // Situation type : option activée, ALÉA en cours de niveau 50 max, joueur niveau 100, libre d'agir.
    private static readonly FateSyncInput Typical = new(
        Enabled: true,
        FateRunning: true,
        FateMaxLevel: 50,
        PlayerLevel: 100,
        AlreadySynced: false,
        CanAct: true,
        Attempts: 0);

    [Fact]
    public void SyncsWhenAboveTheFateLevel() =>
        Assert.Equal(FateSyncAction.Sync, FateSyncRules.Decide(Typical));

    [Fact]
    public void DoesNothingWhenTheOptionIsOff() =>
        Assert.Equal(FateSyncAction.Wait, FateSyncRules.Decide(Typical with { Enabled = false }));

    [Fact]
    public void WaitsUntilTheFateHasStarted() =>
        Assert.Equal(FateSyncAction.Wait, FateSyncRules.Decide(Typical with { FateRunning = false }));

    [Fact]
    public void WaitsWhileThePlayerCannotAct() =>
        Assert.Equal(FateSyncAction.Wait, FateSyncRules.Decide(Typical with { CanAct = false }));

    [Theory]
    [InlineData(50)]
    [InlineData(42)]
    public void NothingToDoWhenThePlayerIsWithinTheFateLevel(int playerLevel) =>
        Assert.Equal(FateSyncAction.Done, FateSyncRules.Decide(Typical with { PlayerLevel = playerLevel }));

    [Fact]
    public void NothingToDoWhenAlreadySynced() =>
        Assert.Equal(FateSyncAction.Done, FateSyncRules.Decide(Typical with { AlreadySynced = true }));

    [Fact]
    public void RetriesUntilTheLastAttempt() =>
        Assert.Equal(FateSyncAction.Sync, FateSyncRules.Decide(Typical with { Attempts = FateSyncRules.MaxAttempts - 1 }));

    [Fact]
    public void GivesUpAfterTheLastAttempt() =>
        Assert.Equal(FateSyncAction.Done, FateSyncRules.Decide(Typical with { Attempts = FateSyncRules.MaxAttempts }));

    [Fact]
    public void ADisabledOptionNeverCountsAsDone() =>
        // Si le joueur active l'option une fois dans l'ALÉA, la synchronisation doit encore pouvoir se faire.
        Assert.Equal(FateSyncAction.Wait, FateSyncRules.Decide(Typical with { Enabled = false, AlreadySynced = true }));
}
