using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Leaves_MedTweaks;

public partial class MedTweaks
{
    private void ApplyAll()
    {
        int entryNumber = 0;
        foreach (Dictionary<MongoId, MedEntry> singleKeyEntry in config.Meds)
        {
            entryNumber++;

            // A list of one-key objects, not a map, so the same source tpl can appear twice
            // (edit it in place in one entry, clone from it in another).
            if (singleKeyEntry.Count != 1)
            {
                Warn($"meds[{entryNumber}]: each entry needs exactly one source-tpl key, found {singleKeyEntry.Count} - skipped");
                continue;
            }

            foreach (KeyValuePair<MongoId, MedEntry> pair in singleKeyEntry)
            {
                ApplyEntry(pair.Key, pair.Value, entryNumber);
            }
        }
    }

    private void ApplyEntry(MongoId sourceId, MedEntry entry, int entryNumber)
    {
        var itemDB = databaseService.GetItems();

        if (!itemDB.TryGetValue(sourceId, out TemplateItem? source))
        {
            Warn($"meds[{entryNumber}]: source med {sourceId} is not in the item DB - skipped");
            return;
        }
        if (source.Properties == null)
        {
            Warn($"meds[{entryNumber}]: source med {sourceId} has no _props - skipped");
            return;
        }

        if (entry.CopyToNew != null)
        {
            MongoId? newId = CloneToNew(source, entry.CopyToNew);
            if (newId == null)
            {
                return;
            }

            ApplyFields(itemDB[newId.Value].Properties!, entry);
            Leaves.Utils.Instance.printColor($"[MedTweaks] cloned {sourceId} -> {newId.Value} \"{entry.CopyToNew.Name}\"", LogTextColor.Green);

            if (entry.CopyToNew.AddToTrader != null)
            {
                AddCloneToTrader(newId.Value, entry.CopyToNew.AddToTrader, entryNumber);
            }
            return;
        }

        ApplyFields(source.Properties, entry);
        Leaves.Utils.Instance.printColor($"[MedTweaks] edited {sourceId} ({source.Name})", LogTextColor.Blue);
    }

    // Null return = the clone failed (already logged). A key already present in ids.jsonc whose
    // item exists means a previous run built it - reuse it, don't clone twice.
    private MongoId? CloneToNew(TemplateItem source, CopySpec spec)
    {
        MongoId newId = idStore.GetOrCreate(spec.IdKey);

        var itemDB = databaseService.GetItems();
        if (itemDB.ContainsKey(newId))
        {
            return newId;
        }

        double price = handbookHelper.GetTemplatePrice(source.Id);
        if (spec.HandbookPriceRoubles != null)
        {
            price = spec.HandbookPriceRoubles.Value;
        }
        double fleaPrice = price;
        if (spec.FleaPriceRoubles != null)
        {
            fleaPrice = spec.FleaPriceRoubles.Value;
        }

        string? handbookParentId = null;
        foreach (HandbookItem handbookItem in databaseService.GetHandbook().Items)
        {
            if (handbookItem.Id == source.Id)
            {
                handbookParentId = handbookItem.ParentId.ToString();
                break;
            }
        }

        var details = new NewItemFromCloneDetails
        {
            ItemTplToClone = source.Id,
            NewId = newId.ToString(),
            ParentId = source.Parent.ToString(),
            HandbookParentId = handbookParentId,
            HandbookPriceRoubles = price,
            FleaPriceRoubles = fleaPrice,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = spec.Name,
                    ShortName = spec.ShortName,
                    Description = spec.Description
                }
            }
        };

        CreateItemResult result = customItemService.CreateItemFromClone(details);
        if (result.Success != true)
        {
            List<string> errors = result.Errors ?? new List<string>();
            Warn($"clone of {source.Id} (\"{spec.Name}\") failed: {string.Join("; ", errors)}");
            return null;
        }

        return newId;
    }

    private void ApplyFields(TemplateItemProperties props, MedEntry entry)
    {
        if (entry.MedUseTime != null) props.MedUseTime = entry.MedUseTime;
        if (entry.MaxHpResource != null) props.MaxHpResource = entry.MaxHpResource;
        if (entry.Weight != null) props.Weight = entry.Weight;
        if (entry.HpResourceRate != null) props.HpResourceRate = entry.HpResourceRate;
        if (entry.StimulatorBuffs != null) props.StimulatorBuffs = entry.StimulatorBuffs;

        if (entry.Effects != null)
        {
            ApplyEffects(props, entry.Effects);
        }
    }

    // Authoritative: the listed keys become the med's entire effects_damage.
    // Empty dict rebuilds it empty; null (block omitted) is left alone by the caller.
    private void ApplyEffects(TemplateItemProperties props, Dictionary<string, EffectEdit> effects)
    {
        Dictionary<DamageEffectType, EffectsDamageProperties>? nativeRows = props.EffectsDamage;
        var rebuilt = new Dictionary<DamageEffectType, EffectsDamageProperties>();

        foreach (KeyValuePair<string, EffectEdit> pair in effects)
        {
            // Enum.TryParse takes the integer key ("0") and the name ("HeavyBleeding") both.
            if (!Enum.TryParse<DamageEffectType>(pair.Key, true, out DamageEffectType effect))
            {
                Warn($"unknown effect key '{pair.Key}' - skipped");
                continue;
            }

            // Seed from the med's native row (shallow copy) so a null EffectEdit field inherits it.
            EffectsDamageProperties row;
            if (nativeRows != null && nativeRows.TryGetValue(effect, out EffectsDamageProperties? nativeRow))
            {
                row = nativeRow with { };
            }
            else
            {
                row = new EffectsDamageProperties();
            }

            EffectEdit edit = pair.Value;
            if (edit.Delay != null) row.Delay = edit.Delay;
            if (edit.Duration != null) row.Duration = edit.Duration;
            if (edit.FadeOut != null) row.FadeOut = edit.FadeOut;
            if (edit.Cost != null) row.Cost = edit.Cost;
            if (edit.HealthPenaltyMin != null) row.HealthPenaltyMin = edit.HealthPenaltyMin;
            if (edit.HealthPenaltyMax != null) row.HealthPenaltyMax = edit.HealthPenaltyMax;

            rebuilt[effect] = row;
        }

        props.EffectsDamage = rebuilt;
    }

    // Stocks a freshly cloned item on a trader as a plain cash sale (Items + BarterScheme + LoyalLevelItems,
    // via the shared LeavesUtils helper). Price defaults to the clone's handbook price in roubles.
    private void AddCloneToTrader(MongoId cloneId, TraderSale sale, int entryNumber)
    {
        if (!databaseService.GetTraders().ContainsKey(sale.Trader))
        {
            Warn($"meds[{entryNumber}]: addToTrader trader {sale.Trader} is not in the trader table - not stocked");
            return;
        }

        if (!Enum.TryParse<CurrencyType>(sale.Currency, true, out CurrencyType currency))
        {
            Warn($"meds[{entryNumber}]: addToTrader currency '{sale.Currency}' is not a known currency - not stocked");
            return;
        }
        MongoId currencyTpl = currency.GetCurrencyTpl();

        double price;
        if (sale.Price != null)
        {
            price = sale.Price.Value;
        }
        else
        {
            price = handbookHelper.GetTemplatePrice(cloneId);
        }

        MongoId assortItemId = Leaves.Utils.Instance.addTrade(sale.Trader, cloneId, currencyTpl, price, sale.LoyaltyLevel);
        if (assortItemId == default)
        {
            Warn($"meds[{entryNumber}]: addToTrader failed - trader {sale.Trader} has no assort");
            return;
        }

        Leaves.Utils.Instance.printColor(
            $"[MedTweaks] stocked {cloneId} on {Leaves.Utils.Instance.getTraderNickname(sale.Trader)} @ {price} {sale.Currency} (LL{sale.LoyaltyLevel})",
            LogTextColor.Green);
    }
}
