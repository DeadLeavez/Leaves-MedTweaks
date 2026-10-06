using System.Text.Json.Serialization;
using Leaves;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace Leaves_MedTweaks;

[ConfigHeader("""
    MedTweaks edits existing medical items and adds new ones cloned from existing ones.
    
    Check https://localhost:6969/webui/db/ while the server is running to search the itemDB for meds.
""")]
public record MedTweaksConfig
{
    [Comment("""
        Duplicate keys are fine because each is its own object. 
        Example: edit Salewa in place AND clone a new med from Salewa using two separate entries.
""")]
    [JsonPropertyName("meds")]
    [AllowedIds(AllowedIdKind.Item)]
    public required List<Dictionary<MongoId, MedEntry>> Meds { get; set; }
}

[FetchSource("/webui/db/item/{key}")]
public record MedEntry
{
    [Comment("Present => clone into a new item and apply the rest to the clone. Absent => edit the original.")]
    [Optional]
    [JsonPropertyName("copyToNew")]
    public CopySpec? CopyToNew { get; set; }

    [Comment("Seconds the heal animation takes.")]
    [SourceField("item._props.medUseTime")]
    [JsonPropertyName("medUseTime")]
    public double? MedUseTime { get; set; }

    [Comment("Total HP pool of the kit.")]
    [SourceField("item._props.MaxHpResource")]
    [JsonPropertyName("maxHpResource")]
    public int? MaxHpResource { get; set; }

    [Comment("Item weight.")]
    [SourceField("item._props.Weight")]
    [JsonPropertyName("weight")]
    public double? Weight { get; set; }

    [Comment("HP restored per healing tick. 0 = the whole effect is applied at once.")]
    [SourceField("item._props.hpResourceRate")]
    [JsonPropertyName("hpResourceRate")]
    public double? HpResourceRate { get; set; }

    [Comment("Stimulator buff key. No idea if this is useful.")]
    [SourceField("item._props.StimulatorBuffs")]
    [JsonPropertyName("stimulatorBuffs")]
    public string? StimulatorBuffs { get; set; }

    [Comment("Ban this med from every chest rig and armoured rig.")]
    [JsonPropertyName("banFromRigs")]
    public bool BanFromRigs { get; set; }

    [Comment("""
    The complete list of damage effects the med can treat.
      0 HeavyBleeding   3 Contusion            6 RadExposure
      1 LightBleeding   4 Intoxication         7 Pain
      2 Fracture        5 LethalIntoxication   8 DestroyedPart
""")]
    [Choices(typeof(DamageEffectType), true)]
    [SourceField("item._props.effects_damage")]
    [JsonPropertyName("effects")]
    public Dictionary<string, EffectEdit>? Effects { get; set; }
}

public record CopySpec
{
    [Comment("Unique id for this items ID. DO NOT CHANGE MID-RUN!")]
    [JsonPropertyName("idKey")]
    public required string IdKey { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("shortName")]
    public required string ShortName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [Comment("Handbook price in roubles. Remove to use source med's handbook price.")]
    [JsonPropertyName("handbookPriceRoubles")]
    public int? HandbookPriceRoubles { get; set; }

    [Comment("Flea starting price in roubles. Remove to use source med's flea price.")]
    [JsonPropertyName("fleaPriceRoubles")]
    public int? FleaPriceRoubles { get; set; }

    [Comment("Present => also add clone to trader. Absent => Item exists, but no one sells it.")]
    [Optional]
    [JsonPropertyName("addToTrader")]
    public TraderSale? AddToTrader { get; set; }
}

// Puts the cloned item on a trader's shop as a plain cash sale. Only used when CopyToNew is set.
public record TraderSale
{
    [Comment("Trader who sells it.")]
    [AllowedIds(AllowedIdKind.Trader)]
    [JsonPropertyName("trader")]
    public required MongoId Trader { get; set; }

    [Comment("Currency to charge in.")]
    [Choices(typeof(CurrencyType))]
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    [Comment("Price in the chosen currency. Uses handbook if missing")]
    [JsonPropertyName("price")]
    public int? Price { get; set; }

    [Comment("Loyalty level the player needs with the trader to see the offer (1-4).")]
    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; } = 1;
}

// One row inside effects_damage
// Every field optional; a null field inherits the med's native value for that effect
public record EffectEdit
{
    [JsonPropertyName("delay")]
    public double? Delay { get; set; }

    [Comment("Seconds the effect is applied/suppressed for.")]
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("fadeOut")]
    public double? FadeOut { get; set; }

    [Comment("HP drawn from the kit pool to cure the effect once.")]
    [JsonPropertyName("cost")]
    public double? Cost { get; set; }

    [JsonPropertyName("healthPenaltyMin")]
    public double? HealthPenaltyMin { get; set; }

    [JsonPropertyName("healthPenaltyMax")]
    public double? HealthPenaltyMax { get; set; }
}
