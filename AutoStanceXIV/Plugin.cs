using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AutoStanceXIV;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/autostance";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AutoStanceXIV");
    private readonly ConfigWindow configWindow;
    private readonly StanceController controller;
    private readonly IDtrBarEntry dtrEntry;

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = LoadConfiguration();
        Configuration.Migrate();
        Loc.Update(Configuration.Language);
        controller = new StanceController(Configuration);

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        PluginInterface.LanguageChanged += OnDalamudLanguageChanged;

        RegisterCommand();

        // Barre d'infos serveur : clic gauche = on/off, clic droit = pause.
        dtrEntry = DtrBar.Get("AutoStanceXIV");
        dtrEntry.OnClick = e =>
        {
            if (e.ClickType == MouseClickType.Right)
                SetMode(StanceMode.None);
            else
                ToggleMode();
        };
        UpdateDtrEntry();
        controller.KindChanged += UpdateDtrEntry;
    }

    // Un fichier de configuration illisible ne doit pas empêcher le plugin de se charger : on repart des réglages par défaut.
    private static Configuration LoadConfiguration()
    {
        try
        {
            return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (System.Exception e)
        {
            Log.Error(e, "Could not read the configuration, falling back to default settings.");
            return new Configuration();
        }
    }

    /// <summary>Type d'instance dans lequel se trouve le joueur.</summary>
    public InstanceKind CurrentKind => controller.CurrentKind;

    /// <summary>Réglages en vigueur là où se trouve le joueur (profil classique, ou celui du type d'instance).</summary>
    public StanceProfile ActiveProfile => Configuration.GetProfile(controller.CurrentKind);

    public void Dispose()
    {
        controller.KindChanged -= UpdateDtrEntry;
        dtrEntry.Remove();
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        controller.Dispose();
    }

    /// <summary>Change le mode là où se trouve le joueur (commande ou barre d'infos serveur).</summary>
    public void SetMode(StanceMode mode)
    {
        ActiveProfile.Mode = mode;
        Configuration.Save();
        OnSettingsChanged();

        if (!Configuration.ChatFeedback)
            return;

        // En mode avancé, le changement ne vaut que pour le type d'instance en cours : on le précise.
        var where = Configuration.AdvancedMode ? $" ({InstanceKinds.Name(CurrentKind)})" : string.Empty;
        ChatGui.Print(Loc.T($"Stance{where}: {Describe(mode)}", $"Stance{where} : {Describe(mode)}"), "AutoStanceXIV");
    }

    public void ToggleMode() => SetMode(ActiveProfile.Mode == StanceMode.Enable ? StanceMode.Disable : StanceMode.Enable);

    /// <summary>À appeler après une modification des réglages : applique le nouvel état et rafraîchit l'affichage.</summary>
    public void OnSettingsChanged()
    {
        controller.RequestApply();
        UpdateDtrEntry();
    }

    public void SetLanguage(PluginLanguage language)
    {
        Configuration.Language = language;
        Configuration.Save();
        RefreshLanguage();
    }

    public void UpdateDtrEntry()
    {
        var mode = ActiveProfile.Mode;
        dtrEntry.Shown = Configuration.ShowInServerBar;
        dtrEntry.Text = mode switch
        {
            StanceMode.Enable => "Stance: ON",
            StanceMode.Disable => "Stance: OFF",
            _ => "Stance: —",
        };

        var where = Configuration.AdvancedMode ? $"\n{InstanceKinds.Name(CurrentKind)}" : string.Empty;
        dtrEntry.Tooltip = Loc.T(
            $"AutoStanceXIV: {Describe(mode)}{where}\nLeft click: on/off — Right click: pause",
            $"AutoStanceXIV : {Describe(mode)}{where}\nClic gauche : on/off — Clic droit : pause");
    }

    public static string Describe(StanceMode mode) => mode switch
    {
        StanceMode.Enable => Loc.T("enabled automatically", "activée automatiquement"),
        StanceMode.Disable => Loc.T("removed automatically", "retirée automatiquement"),
        _ => Loc.T("paused (the plugin leaves it alone)", "en pause (le plugin n'y touche pas)"),
    };

    private void OnDalamudLanguageChanged(string languageCode)
    {
        if (Configuration.Language == PluginLanguage.Auto)
            RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        Loc.Update(Configuration.Language);
        CommandManager.RemoveHandler(CommandName);
        RegisterCommand();
        UpdateDtrEntry();
    }

    private void RegisterCommand()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T(
                "Opens the settings.\n"
                + "/autostance on → keep the stance on\n"
                + "/autostance off → keep the stance off\n"
                + "/autostance toggle → switch between on and off\n"
                + "/autostance pause → stop touching the stance\n"
                + "In advanced mode, these apply to the type of duty you are in.",
                "Ouvre la configuration.\n"
                + "/autostance on → garder la stance activée\n"
                + "/autostance off → garder la stance retirée\n"
                + "/autostance toggle → basculer entre on et off\n"
                + "/autostance pause → ne plus toucher à la stance\n"
                + "En mode avancé, ces commandes s'appliquent au type d'instance où tu te trouves."),
        });
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                configWindow.Toggle();
                break;
            case "on":
            case "activer":
                SetMode(StanceMode.Enable);
                break;
            case "off":
            case "retirer":
                SetMode(StanceMode.Disable);
                break;
            case "toggle":
                ToggleMode();
                break;
            case "pause":
            case "none":
                SetMode(StanceMode.None);
                break;
            default:
                ChatGui.PrintError(
                    Loc.T($"Unknown argument \"{args}\". Use on, off, toggle or pause.",
                          $"Argument inconnu « {args} ». Utilise on, off, toggle ou pause."),
                    "AutoStanceXIV");
                break;
        }
    }
}
