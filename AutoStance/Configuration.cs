using System;
using Dalamud.Configuration;

namespace AutoStance;

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

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public StanceMode Mode { get; set; } = StanceMode.None;
    public TriggerMode Trigger { get; set; } = TriggerMode.Continuous;

    // Moments utilisés quand Trigger == OnEvents.
    public bool TriggerOnZoneOrJob { get; set; } = true;
    public bool TriggerOnPull { get; set; } = false;
    public bool PullBossOnly { get; set; } = true;
    public bool TriggerOnCountdown { get; set; } = false;
    public int CountdownSeconds { get; set; } = 5;
    public bool TriggerOnDutyRecommence { get; set; } = false;

    public PluginLanguage Language { get; set; } = PluginLanguage.Auto;
    public bool OnlyInDuty { get; set; } = false;
    public bool AllowInCombat { get; set; } = true;
    public bool ShowInServerBar { get; set; } = true;
    public bool ChatFeedback { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    /// <summary>Convertit une configuration enregistrée par une version précédente du plugin.</summary>
    public void Migrate()
    {
        if (Version >= 1)
            return;

        // Version 0 : « Au pull » était un troisième mode exclusif (valeur 2).
        if ((int)Trigger == 2)
        {
            Trigger = TriggerMode.OnEvents;
            TriggerOnZoneOrJob = false;
            TriggerOnPull = true;
        }

        Version = 1;
        Save();
    }
}
