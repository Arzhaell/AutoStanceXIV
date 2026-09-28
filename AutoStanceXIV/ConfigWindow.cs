using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoStanceXIV;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("AutoStanceXIV###AutoStanceConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    public override void Draw()
    {
        var configuration = plugin.Configuration;

        ImGui.TextUnformatted(Loc.T("Tank stance:", "Stance de tank :"));
        if (ImGui.RadioButton(Loc.T("Enable automatically", "Activer automatiquement"), configuration.Mode == StanceMode.Enable))
            plugin.SetMode(StanceMode.Enable);
        if (ImGui.RadioButton(Loc.T("Remove automatically", "Retirer automatiquement"), configuration.Mode == StanceMode.Disable))
            plugin.SetMode(StanceMode.Disable);
        if (ImGui.RadioButton(Loc.T("Pause (do nothing)", "Pause (ne rien faire)"), configuration.Mode == StanceMode.None))
            plugin.SetMode(StanceMode.None);

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("When to apply it:", "Quand l'appliquer :"));
        if (ImGui.RadioButton(Loc.T("Always", "En permanence"), configuration.Trigger == TriggerMode.Continuous))
            SetTrigger(TriggerMode.Continuous);
        Tooltip(Loc.T(
            "Puts the stance back in the desired state as soon as it changes,\neven if you change it manually.",
            "Remet la stance dans l'état voulu dès qu'elle en sort,\nmême si tu la changes à la main."));
        if (ImGui.RadioButton(Loc.T("At specific moments:", "À certains moments :"), configuration.Trigger == TriggerMode.OnEvents))
            SetTrigger(TriggerMode.OnEvents);
        Tooltip(Loc.T(
            "Applies the desired state once at the checked moments,\nthen lets you change it manually.",
            "Applique l'état voulu une fois aux moments cochés,\npuis te laisse la changer à la main."));

        ImGui.Indent();
        ImGui.BeginDisabled(configuration.Trigger != TriggerMode.OnEvents);

        Checkbox(Loc.T("Zone change, job change, resurrection", "Entrée en zone, changement de job, résurrection"),
                 configuration.TriggerOnZoneOrJob, v => configuration.TriggerOnZoneOrJob = v);

        Checkbox(Loc.T("Countdown", "Compte à rebours"), configuration.TriggerOnCountdown, v => configuration.TriggerOnCountdown = v);
        Tooltip(Loc.T("So the stance is in place before the first hit.", "Pour que la stance soit en place avant le premier coup."));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        var seconds = configuration.CountdownSeconds;
        if (ImGui.SliderInt("##CountdownSeconds", ref seconds, 1, 30, Loc.T("%d s before the end", "%d s avant la fin")))
            configuration.CountdownSeconds = seconds;
        if (ImGui.IsItemDeactivatedAfterEdit())
            configuration.Save();

        Checkbox(Loc.T("On pull", "Au pull"), configuration.TriggerOnPull, v => configuration.TriggerOnPull = v);
        Tooltip(Loc.T("When combat starts, once per fight.", "Au début du combat, une fois par combat."));
        ImGui.SameLine();
        Checkbox(Loc.T("Bosses only", "Boss uniquement"), configuration.PullBossOnly, v => configuration.PullBossOnly = v);
        Tooltip(Loc.T("Ignores trash packs.", "Ignore les packs d'adds."));

        Checkbox(Loc.T("Duty recommence (after a wipe)", "Reprise de l'instance (après un wipe)"),
                 configuration.TriggerOnDutyRecommence, v => configuration.TriggerOnDutyRecommence = v);
        Tooltip(Loc.T("When the fight restarts at the starting point after a wipe.", "Quand le combat reprend au point de départ après un wipe."));

        ImGui.EndDisabled();
        ImGui.Unindent();

        ImGui.Separator();
        Checkbox(Loc.T("Only in duties (dungeon, raid, trial…)", "Seulement en instance (donjon, raid, défi…)"),
                 configuration.OnlyInDuty, v => configuration.OnlyInDuty = v);
        Checkbox(Loc.T("Allow in combat", "Autoriser en combat"), configuration.AllowInCombat, v => configuration.AllowInCombat = v);
        Tooltip(Loc.T(
            "The \"On pull\" trigger always acts, since it happens in combat.",
            "Le déclencheur « Au pull » agit toujours, puisqu'il a lieu en combat."));
        if (Checkbox(Loc.T("Show in the server info bar", "Afficher dans la barre d'infos serveur"),
                     configuration.ShowInServerBar, v => configuration.ShowInServerBar = v))
            plugin.UpdateDtrEntry();
        Checkbox(Loc.T("Chat message when the mode changes", "Message dans le chat quand le mode change"),
                 configuration.ChatFeedback, v => configuration.ChatFeedback = v);

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("Language:", "Langue :"));
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto", configuration.Language == PluginLanguage.Auto))
            plugin.SetLanguage(PluginLanguage.Auto);
        Tooltip(Loc.T("Follows the Dalamud language.", "Suit la langue de Dalamud."));
        ImGui.SameLine();
        if (ImGui.RadioButton("English", configuration.Language == PluginLanguage.English))
            plugin.SetLanguage(PluginLanguage.English);
        ImGui.SameLine();
        if (ImGui.RadioButton("Français", configuration.Language == PluginLanguage.French))
            plugin.SetLanguage(PluginLanguage.French);
    }

    private bool Checkbox(string label, bool value, Action<bool> setter)
    {
        if (!ImGui.Checkbox(label, ref value))
            return false;

        setter(value);
        plugin.Configuration.Save();
        return true;
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }

    private void SetTrigger(TriggerMode trigger)
    {
        plugin.Configuration.Trigger = trigger;
        plugin.Configuration.Save();
    }
}
