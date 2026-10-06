using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoStanceXIV;

public enum StanceMode
{
    /// <summary>Le plugin ne touche pas à la stance.</summary>
    None,

    /// <summary>La stance doit être activée.</summary>
    Enable,

    /// <summary>La stance doit être retirée.</summary>
    Disable,
}

public enum TriggerMode
{
    /// <summary>Remet la stance dans l'état voulu dès qu'elle en sort.</summary>
    Continuous,

    /// <summary>Applique l'état voulu une fois, aux moments cochés (zone, pull, compte à rebours, reprise).</summary>
    OnEvents,
}

/// <summary>Un jeu de réglages complet : l'état voulu de la stance et quand l'appliquer.</summary>
[Serializable]
public class StanceProfile
{
    public StanceMode Mode { get; set; } = StanceMode.None;
    public TriggerMode Trigger { get; set; } = TriggerMode.Continuous;

    // Moments utilisés quand Trigger == OnEvents.
    public bool TriggerOnZoneChange { get; set; } = true;
    public bool TriggerOnResurrection { get; set; } = true;
    public bool TriggerOnJobChange { get; set; } = true;
    public bool TriggerOnPull { get; set; } = false;
    public bool PullBossOnly { get; set; } = true;
    public bool TriggerOnCountdown { get; set; } = false;
    public int CountdownSeconds { get; set; } = 5;
    public bool TriggerOnDutyRecommence { get; set; } = false;

    // Jusqu'à la 1.1.0, entrée en zone, résurrection et changement de job formaient une seule case.
    // Lue dans les anciens fichiers pour régler les trois cases, jamais réécrite (pas de getter).
    [JsonProperty(LegacyZoneOrJobKey)]
    private bool LegacyTriggerOnZoneOrJob
    {
        set => SetZoneResurrectionAndJob(value);
    }

    internal const string LegacyZoneOrJobKey = "TriggerOnZoneOrJob";

    internal void SetZoneResurrectionAndJob(bool value) =>
        TriggerOnZoneChange = TriggerOnResurrection = TriggerOnJobChange = value;

    public StanceProfile Clone() => (StanceProfile)MemberwiseClone();
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    // 3 : entrée en zone, résurrection et changement de job séparés.
    internal const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>false : le profil Classic s'applique partout. true : un profil par type d'instance.</summary>
    public bool AdvancedMode { get; set; } = false;
    public StanceProfile Classic { get; set; } = new();
    public Dictionary<InstanceKind, StanceProfile> Advanced { get; set; } = [];

    public PluginLanguage Language { get; set; } = PluginLanguage.Auto;
    public bool OnlyInDuty { get; set; } = false;
    public bool AllowInCombat { get; set; } = true;
    public bool ShowInServerBar { get; set; } = true;
    public bool ChatFeedback { get; set; } = true;

    // Reçoit les propriétés des anciennes versions qui n'existent plus à la racine (voir Migrate).
    [JsonExtensionData(WriteData = false)]
    private IDictionary<string, JToken>? legacy;

    public StanceProfile GetProfile(InstanceKind kind) => AdvancedMode ? Advanced[kind] : Classic;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    /// <summary>
    /// Convertit une configuration enregistrée par une version précédente et complète les profils manquants.
    /// Renvoie true si quelque chose a changé et doit être enregistré.
    /// </summary>
    public bool Migrate()
    {
        var changed = false;

        // Versions 0 et 1 : il n'y avait qu'un seul jeu de réglages, stocké à la racine.
        // On se fie à la présence de ces anciennes clés plutôt qu'au numéro de version : une ancienne
        // version du plugin qui relit un fichier récent le réécrit à l'ancien format sans toucher au numéro.
        if (legacy != null && legacy.ContainsKey(nameof(StanceProfile.Mode)))
        {
            Classic.Mode = ReadLegacy(nameof(StanceProfile.Mode), Classic.Mode);
            Classic.SetZoneResurrectionAndJob(ReadLegacy(StanceProfile.LegacyZoneOrJobKey, Classic.TriggerOnZoneChange));
            Classic.TriggerOnPull = ReadLegacy(nameof(StanceProfile.TriggerOnPull), Classic.TriggerOnPull);
            Classic.PullBossOnly = ReadLegacy(nameof(StanceProfile.PullBossOnly), Classic.PullBossOnly);
            Classic.TriggerOnCountdown = ReadLegacy(nameof(StanceProfile.TriggerOnCountdown), Classic.TriggerOnCountdown);
            Classic.CountdownSeconds = ReadLegacy(nameof(StanceProfile.CountdownSeconds), Classic.CountdownSeconds);
            Classic.TriggerOnDutyRecommence = ReadLegacy(nameof(StanceProfile.TriggerOnDutyRecommence), Classic.TriggerOnDutyRecommence);

            // Version 0 : « Au pull » était un troisième mode exclusif (valeur 2).
            var trigger = ReadLegacy(nameof(StanceProfile.Trigger), 0);
            if (trigger == 2)
            {
                Classic.Trigger = TriggerMode.OnEvents;
                Classic.SetZoneResurrectionAndJob(false);
                Classic.TriggerOnPull = true;
            }
            else
            {
                Classic.Trigger = (TriggerMode)trigger;
            }

            changed = true;
        }

        legacy = null;
        if (Version != CurrentVersion)
        {
            Version = CurrentVersion;
            changed = true;
        }

        // Les profils avancés partent des réglages classiques : passer en mode avancé ne change rien
        // tant qu'on ne les a pas modifiés.
        foreach (var kind in InstanceKinds.All)
        {
            if (Advanced.ContainsKey(kind))
                continue;

            var profile = Classic.Clone();
            if (kind == InstanceKind.OpenWorld && OnlyInDuty)
                profile.Mode = StanceMode.None;
            Advanced[kind] = profile;
            changed = true;
        }

        return changed;
    }

    private T ReadLegacy<T>(string key, T fallback) =>
        legacy != null && legacy.TryGetValue(key, out var token) && token.ToObject<T>() is { } value ? value : fallback;
}
