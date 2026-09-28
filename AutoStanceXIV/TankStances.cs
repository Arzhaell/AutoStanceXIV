using System.Collections.Generic;

namespace AutoStanceXIV;

/// <summary>Stance d'un tank : action qui l'active, action qui la dissipe, et buff visible sur le joueur.</summary>
public readonly record struct TankStance(uint ActionId, uint ReleaseActionId, uint StatusId);

public static class TankStances
{
    private static readonly TankStance IronWill = new(28, 32065, 79);        // Volonté de fer
    private static readonly TankStance Defiance = new(48, 32066, 91);        // Défi
    private static readonly TankStance Grit = new(3629, 32067, 743);         // Férocité
    private static readonly TankStance RoyalGuard = new(16142, 32068, 1833); // Garde royale

    // Clé : ClassJob.RowId
    private static readonly Dictionary<uint, TankStance> ByClassJob = new()
    {
        [1] = IronWill,    // Gladiateur
        [19] = IronWill,   // Paladin
        [3] = Defiance,    // Maraudeur
        [21] = Defiance,   // Guerrier
        [32] = Grit,       // Chevalier noir
        [37] = RoyalGuard, // Pistosabreur
    };

    public static bool TryGet(uint classJobId, out TankStance stance) => ByClassJob.TryGetValue(classJobId, out stance);
}
