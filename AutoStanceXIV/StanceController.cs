using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AutoStanceXIV;

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

    // Type d'instance en cours, recalculé seulement quand l'instance change.
    private bool kindKnown;
    private bool lastInDuty;
    private uint lastDutyId;

    /// <summary>Type d'instance dans lequel se trouve le joueur.</summary>
    public InstanceKind CurrentKind { get; private set; } = InstanceKind.OpenWorld;

    /// <summary>Déclenché quand le joueur change de type d'instance.</summary>
    public event System.Action? KindChanged;

    // Réglages en vigueur : le profil classique, ou celui du type d'instance en mode avancé.
    private StanceProfile Profile => configuration.GetProfile(CurrentKind);

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

        // En mode avancé, ce qui était en attente pour l'ancien type d'instance ne vaut plus pour le nouveau.
        var kindChanged = UpdateInstanceKind() && configuration.AdvancedMode;
        if (kindChanged)
            ClearPending();
        var profile = Profile;

        DetectZoneJobAndRevive(profile, kindChanged, player.ClassJob.RowId, player.IsDead);
        DetectPull(profile);
        DetectCountdown(profile);
        DetectDutyRecommence(profile);
        if (player.IsDead)
            return;

        if (profile.Mode == StanceMode.None)
            return;
        if (profile.Trigger == TriggerMode.OnEvents && !pending)
            return;

        if (!TankStances.TryGet(player.ClassJob.RowId, out var stance))
        {
            ClearPending();
            return;
        }

        var wantActive = profile.Mode == StanceMode.Enable;
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

    /// <summary>Met à jour CurrentKind. Renvoie true si le type d'instance vient de changer.</summary>
    private bool UpdateInstanceKind()
    {
        var inDuty = InstanceKinds.IsInDuty();
        var dutyId = inDuty ? InstanceKinds.CurrentDutyId(Plugin.ClientState.TerritoryType) : 0;
        if (kindKnown && inDuty == lastInDuty && dutyId == lastDutyId)
            return false;

        kindKnown = true;
        lastInDuty = inDuty;
        lastDutyId = dutyId;

        var kind = InstanceKinds.Classify(inDuty, dutyId);
        Plugin.Log.Debug($"Instance: {kind} (duty {dutyId}).");
        if (kind == CurrentKind)
            return false;

        CurrentKind = kind;
        KindChanged?.Invoke();
        return true;
    }

    private void DetectZoneJobAndRevive(StanceProfile profile, bool kindChanged, uint classJob, bool isDead)
    {
        uint territory = Plugin.ClientState.TerritoryType;

        // Un changement de type d'instance compte comme une entrée en zone : le jeu peut signaler l'instance
        // un instant après l'arrivée du joueur, et c'est alors le nouveau profil qui doit s'appliquer.
        var zoneChanged = !wasPresent || kindChanged || territory != lastTerritory;
        var jobChanged = wasPresent && classJob != lastClassJob;
        var revived = wasDead && !isDead;

        if (zoneChanged && profile.TriggerOnZoneChange)
            Trigger("zone change");
        if (jobChanged && profile.TriggerOnJobChange)
            Trigger("job change");
        if (revived && profile.TriggerOnResurrection)
            Trigger("resurrection");

        wasPresent = true;
        wasDead = isDead;
        lastTerritory = territory;
        lastClassJob = classJob;
    }

    private void DetectPull(StanceProfile profile)
    {
        if (!Plugin.Condition[ConditionFlag.InCombat])
        {
            pullDetected = false;
            pendingFromPull = false;
            return;
        }

        if (profile.Trigger != TriggerMode.OnEvents || !profile.TriggerOnPull || pullDetected)
            return;

        // Avec « boss uniquement », on continue de chercher pendant tout le combat :
        // ça couvre aussi un boss qui rejoint un combat déjà commencé contre des adds.
        if (!profile.PullBossOnly || IsBossEngaged())
        {
            pullDetected = true;
            pendingFromPull = true;
            Trigger(profile.PullBossOnly ? "boss pull" : "combat start");
        }
    }

    private void DetectCountdown(StanceProfile profile)
    {
        var countdown = AgentCountDownSettingDialog.Instance();
        if (countdown == null || !countdown->Active || countdown->TimeRemaining <= 0)
        {
            countdownDetected = false;
            return;
        }

        if (!profile.TriggerOnCountdown || countdownDetected)
            return;

        if (countdown->TimeRemaining <= profile.CountdownSeconds)
        {
            countdownDetected = true;
            Trigger($"countdown ({countdown->TimeRemaining:0.0} s left)");
        }
    }

    private void DetectDutyRecommence(StanceProfile profile)
    {
        if (!dutyRecommenced)
            return;

        dutyRecommenced = false;
        if (profile.TriggerOnDutyRecommence)
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
        // En mode avancé, « hors instance » a son propre profil : l'option ne s'applique qu'au mode classique.
        if (!configuration.AdvancedMode && configuration.OnlyInDuty && !InstanceKinds.IsInDuty())
            return false;
        if (!configuration.AllowInCombat && !pendingFromPull && Plugin.Condition[ConditionFlag.InCombat])
            return false;
        return true;
    }
}
