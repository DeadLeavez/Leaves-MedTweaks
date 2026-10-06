# Leaves-MedTweaks

Adjusts existing medical items and adds new ones cloned from existing ones. Everything is
driven by `config/config.jsonc`.

## Requires

- SPT 4.0.x
- Leaves-LeavesUtil

## Config

`meds` is a list of one-key objects. The key is the source med's item tpl; the value is the
set of changes:

- No `copyToNew` → the source med is edited in place.
- `copyToNew` present → a new item is cloned from the source and the changes apply to the
  clone; the source is untouched.

Editable fields: `medUseTime`, `maxHpResource`, `hpResourceRate`, `stimulatorBuffs`, and
per-effect edits under `effects` (`HeavyBleeding`, `LightBleeding`, `Fracture`, `Contusion`,
`RadExposure`, `Pain`, `DestroyedPart`, `Intoxication`) with `cost` / `duration` / `delay` /
`fadeOut` / `healthPenaltyMin` / `healthPenaltyMax`, or `"remove": true` to drop an effect.

Any field left out keeps the source med's value.
