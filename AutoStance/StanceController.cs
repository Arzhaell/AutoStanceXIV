using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AutoStance;

/// <summary>Surveille la stance du joueur et la remet dans l'état choisi dans la configuration.</summary>
public sealed unsafe class StanceController : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DelayAfterUse = TimeSpan.FromSeconds(2.5);

    // États pendant lesquels on ne doit pas utiliser d'action.
    private static readonly ConditionFlag[] BlockingConditions =
    [
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedSummoningBell,
        ConditionFlag.Occupied,
        ConditionFlag.Occupied30,
        ConditionFlag.Occupied33,
        ConditionFlag.Occupied38,
        ConditionFlag.Occupied39,
        ConditionFlag.Mounted,
        ConditionFlag.RidingPillion,
        ConditionFlag.Mounting,
        ConditionFlag.Mounting71,
        ConditionFlag.Jumping,
        ConditionFlag.Jumping61,
        ConditionFlag.Casting,
        ConditionFlag.Casting87,
        ConditionFlag.Unconscious,
    ];

    private readonly Configuration configuration;
    private readonly IDutyState.DutyRecommencedDelegate onDutyRecommenced;
    private DateTime nextCheck = DateTime.MinValue;

    // Mode OnEvents : true quand un moment coché demande d'appliquer l'état voulu.
    private bool pending;

    // Le pull a lieu en combat par définition : il passe outre l'option « Autoriser en combat ».
    private bool pendingFromPull;

    // Chaque moment ne se déclenche qu'une fois par combat / par compte à rebours.
    private bool pullDetected;
    private bool countdownDetected;
    private bool dutyRecommenced;

    private bool wasPresent;
    private bool wasDead;
    private uint lastTerritory;
    private uint lastClassJob;

    public StanceController(Configuration configuration)
    {
        this.configuration = configuration;
        onDutyRecommenced = _ => dutyRecommenced = true;

        Plugin.Framework.Update += OnUpdate;
        Plugin.DutyState.DutyRecommenced += onDutyRecommenced;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.DutyState.DutyRecommenced -= onDutyRecommenced;
    }

    /// <summary>Demande d'appliquer l'état voulu au prochain moment possible (utile en mode OnEvents).</summary>
    public void RequestApply() => pending = true;

    private void OnUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < nextCheck)
            return;
        nextCheck = now + CheckInterval;

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            wasPresent = false;
            return;
        }

        DetectZoneJobAndRevive(player.ClassJob.RowId, player.IsDead);
        DetectPull();
        DetectCountdown();
        DetectDutyRecommence();
        if (player.IsDead)
            return;

        if (configuration.Mode == StanceMode.None)
            return;
        if (configuration.Trigger == TriggerMode.OnEvents && !pending)
            return;

        if (!TankStances.TryGet(player.ClassJob.RowId, out var stance))
        {
            ClearPending();
            return;
        }

        var wantActive = configuration.Mode == StanceMode.Enable;
        var isActive = player.StatusList.Any(s => s.StatusId == stance.StatusId);
        if (isActive == wantActive)
        {
            ClearPending();
            return;
        }

        if (!CanActNow())
            return;

        var actionManager = ActionManager.Instance();
        if (actionManager == null || actionManager->AnimationLock > 0)
            return;

        var actionId = wantActive ? stance.ActionId : stance.ReleaseActionId;
        var status = actionManager->GetActionStatus(ActionType.Action, actionId);
        if (status != 0)
        {
            Plugin.Log.Verbose($"Action {actionId} unavailable (status {status}).");
            return;
        }

        if (actionManager->UseAction(ActionType.Action, actionId))
        {
            Plugin.Log.Debug($"Stance {(wantActive ? "enabled" : "removed")} (action {actionId}).");
            nextCheck = now + DelayAfterUse;
        }
    }

    private void DetectZoneJobAndRevive(uint classJob, bool isDead)
    {
        uint territory = Plugin.ClientState.TerritoryType;

        var changed = !wasPresent || territory != lastTerritory || classJob != lastClassJob || (wasDead && !isDead);
        if (changed && configuration.TriggerOnZoneOrJob)
            Trigger("zone change, job change or resurrection");

        wasPresent = true;
        wasDead = isDead;
        lastTerritory = territory;
        lastClassJob = classJob;
    }

    private void DetectPull()
    {
        if (!Plugin.Condition[ConditionFlag.InCombat])
        {
            pullDetected = false;
            pendingFromPull = false;
            return;
        }

        if (configuration.Trigger != TriggerMode.OnEvents || !configuration.TriggerOnPull || pullDetected)
            return;

        // Avec « boss uniquement », on continue de chercher pendant tout le combat :
        // ça couvre aussi un boss qui rejoint un combat déjà commencé contre des adds.
        if (!configuration.PullBossOnly || IsBossEngaged())
        {
            pullDetected = true;
            pendingFromPull = true;
            Trigger(configuration.PullBossOnly ? "boss pull" : "combat start");
        }
    }

    private void DetectCountdown()
    {
        var countdown = AgentCountDownSettingDialog.Instance();
        if (countdown == null || !countdown->Active || countdown->TimeRemaining <= 0)
        {
            countdownDetected = false;
            return;
        }

        if (!configuration.TriggerOnCountdown || countdownDetected)
            return;

        if (countdown->TimeRemaining <= configuration.CountdownSeconds)
        {
            countdownDetected = true;
            Trigger($"countdown ({countdown->TimeRemaining:0.0} s left)");
        }
    }

    private void DetectDutyRecommence()
    {
        if (!dutyRecommenced)
            return;

        dutyRecommenced = false;
        if (configuration.TriggerOnDutyRecommence)
            Trigger("duty recommence");
    }

    // Même heuristique que RotationSolver : les boss ont un rang 2 ou 6 dans BNpcBase.
    private static bool IsBossEngaged()
    {
        var bnpcBases = Plugin.DataManager.GetExcelSheet<BNpcBase>();
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is IBattleNpc npc
                && !npc.IsDead
                && npc.StatusFlags.HasFlag(StatusFlags.InCombat)
                && bnpcBases.TryGetRow(npc.BaseId, out var row)
                && row.Rank is 2 or 6)
                return true;
        }

        return false;
    }

    private void Trigger(string reason)
    {
        pending = true;
        Plugin.Log.Debug($"Trigger: {reason}");
    }

    private void ClearPending()
    {
        pending = false;
        pendingFromPull = false;
    }

    private bool CanActNow()
    {
        if (Plugin.ClientState.IsPvP)
            return false;
        if (Plugin.Condition.Any(BlockingConditions))
            return false;
        if (configuration.OnlyInDuty && !Plugin.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95))
            return false;
        if (!configuration.AllowInCombat && !pendingFromPull && Plugin.Condition[ConditionFlag.InCombat])
            return false;
        return true;
    }
}
