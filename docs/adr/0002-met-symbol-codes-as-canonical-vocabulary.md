# MET Norway's symbol codes are the canonical weather vocabulary

Every field in the common cross-Provider shape ports cleanly except one: the weather symbol. MET emits names like `partlycloudy_day`, Open-Meteo emits WMO codes `0–99`, others differ again. We adopt **MET Norway's symbol names as our own vocabulary**; any future Provider maps into it.

## Why

Yr's official weather icon set is keyed by exactly these names and openly licensed, so adopting MET's vocabulary makes icons free. MET's set is also well documented and finer-grained (~40 values) than most alternatives, so mapping other Providers into it is mostly lossless in the direction that matters.

The alternative — inventing a neutral Condition enum — is architecturally cleaner, since it privileges no Provider. It was rejected because it costs a mapping for MET on day one, an icon set we would have to source or draw, and a lossy squeeze of 40 codes into roughly 8, all to solve a symmetry problem we do not yet have.

## Consequences

- **One Provider's language is everyone's language.** In a project whose purpose is comparing Providers, this is a real asymmetry and should be recognised as a deliberate shortcut, not a principle.
- A Provider whose conditions do not map into MET's set will force this decision to be revisited. That is the trigger to introduce a neutral vocabulary.
- Because raw Snapshots are stored verbatim (see ADR-0001), remapping every Provider into a new vocabulary later is a code change plus reprocessing — not a data migration. That is what makes this shortcut affordable.
