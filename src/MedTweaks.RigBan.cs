using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Logging;

namespace Leaves_MedTweaks;

// Own IOnLoad, ordered after MedTweaks' main pass (config + clone ids) and after content mods add their rigs.
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 99000)]
public class MedTweaksRigBan(MedTweaks medTweaks) : IOnLoad
{
    // covers plain chest rigs and plate carriers
    private static readonly MongoId ChestRigNode = new("5448e5284bdc2dcb718b4567");

    public Task OnLoad()
    {
        List<MongoId> targets = medTweaks.CollectRigBanTargets();
        if (targets.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (MongoId tpl in targets)
        {
            int touched = Leaves.Utils.Instance.addToGridBlacklist(tpl, ChestRigNode);
            Leaves.Utils.Instance.printColor(
                $"[MedTweaks] rig-ban: {tpl} blacklisted from {touched} rig template(s)",
                LogTextColor.Blue);
        }

        return Task.CompletedTask;
    }
}

public partial class MedTweaks
{
    // clone id for a copyToNew entry, else the source tpl.
    internal List<MongoId> CollectRigBanTargets()
    {
        var targets = new List<MongoId>();
        foreach (Dictionary<MongoId, MedEntry> singleKeyEntry in config.Meds)
        {
            foreach (KeyValuePair<MongoId, MedEntry> pair in singleKeyEntry)
            {
                if (!pair.Value.BanFromRigs)
                {
                    continue;
                }

                if (pair.Value.CopyToNew != null)
                {
                    targets.Add(idStore.GetOrCreate(pair.Value.CopyToNew.IdKey));
                }
                else
                {
                    targets.Add(pair.Key);
                }
            }
        }

        return targets;
    }
}
