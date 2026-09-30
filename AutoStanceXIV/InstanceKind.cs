using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AutoStanceXIV;

/// <summary>Groupes d'instances qui ont chacun leurs réglages en mode avancé.</summary>
public enum InstanceKind
{
    /// <summary>Hors instance (ALÉA, monde ouvert), et tout ce qui n'entre pas dans les autres groupes.</summary>
    OpenWorld,

    /// <summary>Donjons, y compris donjons spéciaux, donjons sans fond, opérations de guilde et chasses aux trésors.</summary>
    Dungeon,

    /// <summary>Défis : normaux, extrêmes et irréels.</summary>
    Trial,

    /// <summary>Raids à 8 : normaux, sadiques et fatals.</summary>
    Raid,

    /// <summary>Raids en alliance à 24, y compris chaotiques.</summary>
    Alliance,
}

internal static unsafe class InstanceKinds
{
    public static readonly InstanceKind[] All = Enum.GetValues<InstanceKind>();

    // Lignes de la feuille ContentType.
    private const uint ContentTypeDungeon = 2;
    private const uint ContentTypeGuildhest = 3;
    private const uint ContentTypeTrial = 4;
    private const uint ContentTypeRaid = 5;
    private const uint ContentTypeTreasureHunt = 9;
    private const uint ContentTypeDeepDungeon = 21;
    private const uint ContentTypeUltimate = 28;
    private const uint ContentTypeSpecialDungeon = 30; // donjons à embranchements / critérium
    private const uint ContentTypeChaotic = 37;

    /// <summary>Nom du groupe, pour les phrases (chat, « tu es actuellement dans… »).</summary>
    public static string Name(InstanceKind kind) => kind switch
    {
        InstanceKind.Dungeon => Loc.T("Dungeon", "Donjon"),
        InstanceKind.Trial => Loc.T("Trial", "Défi"),
        InstanceKind.Raid => Loc.T("Raid", "Raid"),
        InstanceKind.Alliance => Loc.T("Alliance raid", "Raid en alliance"),
        _ => Loc.T("Outside of duties", "Hors instance"),
    };

    /// <summary>Libellé de l'onglet dans la fenêtre de configuration.</summary>
    public static string TabLabel(InstanceKind kind) => kind switch
    {
        InstanceKind.Dungeon => Loc.T("Dungeons", "Donjon"),
        InstanceKind.Trial => Loc.T("Trials", "Défis"),
        InstanceKind.Raid => Loc.T("Raids", "Raid"),
        InstanceKind.Alliance => Loc.T("Alliance", "Alliance"),
        _ => Loc.T("Open world", "ALÉA"),
    };

    /// <summary>Ce que le groupe contient, pour l'infobulle de l'onglet.</summary>
    public static string Description(InstanceKind kind) => kind switch
    {
        InstanceKind.Dungeon => Loc.T(
            "Dungeons, including variant and deep dungeons,\nguildhests and treasure dungeons.",
            "Donjons, y compris donjons spéciaux et sans fond,\nopérations de guilde et chasses aux trésors."),
        InstanceKind.Trial => Loc.T("Trials: normal, extreme and unreal.", "Défis : normaux, extrêmes et irréels."),
        InstanceKind.Raid => Loc.T("8-player raids: normal, savage and ultimate.", "Raids à 8 : normaux, sadiques et fatals."),
        InstanceKind.Alliance => Loc.T("24-player alliance raids, including chaotic.", "Raids en alliance à 24, y compris chaotiques."),
        _ => Loc.T(
            "Everything outside of duties: FATEs, open world,\nfield operations (Eureka, Bozja…), and anything else.",
            "Tout ce qui est hors instance : ALÉA, monde ouvert,\nopérations de terrain (Eurêka, Bozja…), et tout le reste."),
    };

    public static bool IsInDuty() =>
        Plugin.Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);

    /// <summary>Identifiant de l'instance en cours (ligne ContentFinderCondition), 0 si le jeu n'en indique aucune.</summary>
    public static uint CurrentDutyId(uint territoryId)
    {
        var gameMain = GameMain.Instance();
        uint dutyId = gameMain != null ? gameMain->CurrentContentFinderConditionId : 0u;
        if (dutyId == 0 && Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
            dutyId = territory.ContentFinderCondition.RowId;
        return dutyId;
    }

    public static InstanceKind Classify(bool inDuty, uint dutyId)
    {
        if (!inDuty || dutyId == 0 || !Plugin.DataManager.GetExcelSheet<ContentFinderCondition>().TryGetRow(dutyId, out var duty))
            return InstanceKind.OpenWorld;

        switch (duty.ContentType.RowId)
        {
            case ContentTypeDungeon:
            case ContentTypeSpecialDungeon:
            case ContentTypeDeepDungeon:
            case ContentTypeGuildhest:
            case ContentTypeTreasureHunt:
                return InstanceKind.Dungeon;
            case ContentTypeTrial:
                return InstanceKind.Trial;
            case ContentTypeRaid:
                // Les raids en alliance sont des « Raids » à plusieurs équipes.
                return duty.ContentMemberType.ValueNullable?.PartyCount > 1 ? InstanceKind.Alliance : InstanceKind.Raid;
            case ContentTypeUltimate:
                return InstanceKind.Raid;
            case ContentTypeChaotic:
                return InstanceKind.Alliance;
            default:
                return InstanceKind.OpenWorld;
        }
    }
}
