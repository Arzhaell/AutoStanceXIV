using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoStanceXIV;

/// <summary>Ce qu'il faut faire pour l'ALÉA dans lequel se trouve le joueur.</summary>
internal enum FateSyncAction
{
    /// <summary>Rien pour l'instant : option coupée, ALÉA pas encore lancé, joueur occupé…</summary>
    Wait,

    /// <summary>ALÉA réglé (synchronisé, synchronisation inutile ou abandonnée) : on n'y revient qu'après en être sorti.</summary>
    Done,

    /// <summary>Lancer la synchronisation.</summary>
    Sync,
}

/// <summary>Ce que le plugin a lu dans le jeu pour décider.</summary>
internal readonly record struct FateSyncInput(
    bool Enabled,
    bool FateRunning,
    int FateMaxLevel,
    int PlayerLevel,
    bool AlreadySynced,
    bool CanAct,
    int Attempts);

internal static class FateSyncRules
{
    /// <summary>Nombre d'essais par ALÉA avant d'abandonner (le jeu peut refuser sans qu'on sache pourquoi).</summary>
    public const int MaxAttempts = 3;

    public static FateSyncAction Decide(in FateSyncInput input)
    {
        if (!input.Enabled || !input.FateRunning)
            return FateSyncAction.Wait;

        // Déjà synchronisé en entrant (à la main par exemple), ou ALÉA de son niveau : rien à faire.
        // Si le joueur se désynchronise ensuite, on le laisse faire jusqu'au prochain ALÉA.
        if (input.AlreadySynced || input.PlayerLevel <= input.FateMaxLevel)
            return FateSyncAction.Done;

        if (input.Attempts >= MaxAttempts)
            return FateSyncAction.Done;

        return input.CanAct ? FateSyncAction.Sync : FateSyncAction.Wait;
    }
}

/// <summary>Option : synchronise le niveau du joueur quand il entre dans la zone d'un ALÉA.</summary>
public sealed unsafe class FateSync : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    // Commande officielle du jeu, la même que le bouton « Synchroniser » des ALÉA.
    private const string LevelSyncCommand = "/levelsync on";

    // États pendant lesquels on attend : la commande serait refusée ou n'a pas de sens.
    // Monture comprise : on synchronise quand le joueur descend pour combattre.
    private static readonly ConditionFlag[] BlockingConditions =
    [
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.Occupied,
        ConditionFlag.Mounted,
        ConditionFlag.RidingPillion,
        ConditionFlag.Mounting,
        ConditionFlag.Mounting71,
        ConditionFlag.Unconscious,
    ];

    private readonly Configuration configuration;
    private DateTime nextCheck = DateTime.MinValue;

    // ALÉA dans lequel se trouve le joueur, et ce qu'on y a déjà fait.
    private ushort currentFateId;
    private bool done;
    private int attempts;

    public FateSync(Configuration configuration)
    {
        this.configuration = configuration;
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;

    private void OnUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < nextCheck)
            return;
        nextCheck = now + CheckInterval;

        var manager = FateManager.Instance();
        var fate = manager != null ? manager->CurrentFate : null;
        var fateId = fate != null ? fate->FateId : (ushort)0;

        // Nouvel ALÉA (ou sortie de l'ALÉA) : on repart de zéro.
        if (fateId != currentFateId)
        {
            currentFateId = fateId;
            done = false;
            attempts = 0;
        }

        if (fateId == 0 || done)
            return;

        var input = new FateSyncInput(
            Enabled: configuration.AutoFateSync,
            FateRunning: fate->State == FateState.Running,
            FateMaxLevel: fate->MaxLevel,
            PlayerLevel: Plugin.PlayerState.Level,
            AlreadySynced: manager->SyncedFateId == fateId || Plugin.PlayerState.IsLevelSynced,
            CanAct: CanAct(),
            Attempts: attempts);

        switch (FateSyncRules.Decide(input))
        {
            case FateSyncAction.Done:
                done = true;
                if (attempts >= FateSyncRules.MaxAttempts && !input.AlreadySynced)
                    Plugin.Log.Warning($"FATE {fateId}: level sync still not active after {attempts} attempts, giving up.");
                break;

            case FateSyncAction.Sync:
                attempts++;
                Plugin.Log.Debug($"FATE {fateId}: level sync (attempt {attempts}, level {input.PlayerLevel} > max {input.FateMaxLevel}).");
                SendCommand(LevelSyncCommand);
                nextCheck = now + RetryDelay;
                break;
        }
    }

    private static bool CanAct()
    {
        if (Plugin.ClientState.IsPvP || InstanceKinds.IsInDuty())
            return false;
        return !Plugin.Condition.Any(BlockingConditions);
    }

    // Comme si le joueur tapait la commande dans le chat (sans l'ajouter à l'historique du chat).
    private static void SendCommand(string command)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
            return;

        var message = Utf8String.FromString(command);
        try
        {
            uiModule->ProcessChatBoxEntry(message);
        }
        finally
        {
            message->Dtor(true);
        }
    }
}
