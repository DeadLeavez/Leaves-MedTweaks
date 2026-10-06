using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;

namespace Leaves_MedTweaks;

// Edits existing medical items and clones new ones from them, driven entirely by config.
// PostSptModLoader so meds added by content mods (WTT etc.) are already in the DB and can be
// targeted too. Singleton so MedTweaksConfigApi injects this same instance to read and replace its live config.
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public partial class MedTweaks(
    DatabaseService databaseService,
    ModHelper modHelper,
    HandbookHelper handbookHelper,
    CustomItemService customItemService)
    : IOnLoad
{
    private MedTweaksConfig config = null!;
    private string modPath = null!;
    private Leaves.IdStore idStore = null!;

    public Task OnLoad()
    {
        modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        config = modHelper.GetJsonDataFromFile<MedTweaksConfig>(modPath, "config/config.jsonc");
        idStore = new Leaves.IdStore(modPath, "config/ids.jsonc");

        Leaves.Utils.Instance.printColor("[MedTweaks] starting");

        ApplyAll();

        idStore.Save();
        SaveConfig();

        return Task.CompletedTask;
    }

    // EffectKeyLabel writes the DamageEffectType name as an inline comment on each "0".."8" effects key.
    internal void SaveConfig()
    {
        Leaves.Utils.Instance.saveJsoncWithComments(config, modPath, "config/config.jsonc", EffectKeyLabel);
    }

    private static string? EffectKeyLabel(Leaves.LabelRequest request)
    {
        if (request.PropertyName != "effects" || request.Target != Leaves.LabelTarget.DictionaryKey)
        {
            return null;
        }
        if (Enum.TryParse<SPTarkov.Server.Core.Models.Enums.DamageEffectType>(request.Key, true, out var effect))
        {
            return effect.ToString();
        }
        return null;
    }

    private void Warn(string message)
    {
        Leaves.Utils.Instance.printColor("[MedTweaks] " + message, LogTextColor.Yellow);
    }
}
