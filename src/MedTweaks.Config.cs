using System.ComponentModel.DataAnnotations;

namespace Leaves_MedTweaks;

// Web config editor surface (routes in MedTweaksConfigApi). Split from the apply engine: this is
// read / validate / persist only. Nothing here touches the live item DB - MedTweaks mutates meds
// and clones new items solely in OnLoad, so an edit lands on the next server startup.
public partial class MedTweaks
{
    // One config save at a time, so two concurrent POSTs can't interleave the file write.
    private readonly object saveLock = new();

    // GET /medtweaks/config -> { Schema, Current }.
    public MedTweaksConfig CurrentConfig
    {
        get { return config; }
    }

    // POST /medtweaks/config. Parse -> validate -> swap the in-memory copy (so a follow-up GET shows
    // the saved values) -> write to disk. Deliberately does NOT re-apply: field edits, effect edits
    // and new clones all take effect only when the server next boots and OnLoad runs.
    public Leaves.HttpUtils.ConfigApplyResult ApplyConfigJson(string newConfigJson)
    {
        MedTweaksConfig? incomingConfig = Leaves.Utils.Instance.stringToJson<MedTweaksConfig>(newConfigJson);
        if (incomingConfig == null)
        {
            return Leaves.HttpUtils.ConfigApplyResult.Failure("could not parse config");
        }

        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(incomingConfig, new ValidationContext(incomingConfig), errors, true))
        {
            var messages = new List<string>();
            foreach (ValidationResult error in errors)
            {
                messages.Add(error.ErrorMessage ?? "invalid value");
            }
            return Leaves.HttpUtils.ConfigApplyResult.Failure(messages.ToArray());
        }

        List<string> idProblems = Leaves.Utils.Instance.CheckAllowedIds(incomingConfig);
        if (idProblems.Count > 0)
        {
            return Leaves.HttpUtils.ConfigApplyResult.Failure(idProblems.ToArray());
        }

        lock (saveLock)
        {
            Interlocked.Exchange(ref config, incomingConfig);
            SaveConfig();
        }

        return Leaves.HttpUtils.ConfigApplyResult.Success("saved to disk; changes take effect the next time the server starts");
    }
}
