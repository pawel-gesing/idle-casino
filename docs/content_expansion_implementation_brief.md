# Content Expansion v2 — implementation brief

Status: approved design input for a future implementation. This document is not the implementation.

## Objective

Expand Distillery Discovery from 15 to 175 recipes while keeping experiments surprising but semantically constrained. Add ingredient groups, recipe eligibility requirements, additive outcome weights, and a scalable three-slot contract system. Preserve the existing timed jobs, production, mastery, rarity roll, pending-result claim, save/load, offline progress and localization behavior.

## Canonical ingredients and groups

An ingredient may belong to one group. Groups are configuration data with stable IDs and localized names. Group membership satisfies group recipe requirements but does not count against the direct-recipe cap for epic/legendary ingredients.

| Group ID | PL | Ingredients |
|---|---|---|
| `grain` | Zboża | barley, wheat, rye |
| `sweetener` | Słodziwa | honey, cane sugar, molasses |
| `fruit` | Owoce | plum, apple, grape, orange (including peel) |
| `herb` | Zioła | juniper, wormwood, angelica |
| `spice` | Przyprawy | anise, pepper, vanilla, tonka |

Ungrouped: hops, peat, oak, amber, mastic.

Use stable ingredient IDs following the existing convention, e.g. `ingredient_barley_common`. Exact suffixes may reflect rarity, but references must remain internally consistent.

Rarity allocation:

- common: barley, wheat, rye, honey, cane sugar, molasses, plum, apple, grape, orange;
- rare: juniper, wormwood, anise, pepper, hops, peat, oak;
- epic: angelica, vanilla, tonka;
- legendary: amber, mastic.

Use grounded Polish names. Do not introduce fantasy materials such as ghost mushrooms, star dew or dragon blood. Display names: Jęczmień, Pszenica, Żyto, Miód, Cukier trzcinowy, Melasa, Śliwka, Jabłko, Winogrona, Pomarańcza, Jałowiec, Piołun, Arcydzięgiel, Anyż, Pieprz, Wanilia, Tonka, Chmiel, Torf, Dębina, Bursztyn, Mastyks. Provide natural English translations.

## Recipe eligibility and weighted outcomes

Experiments still select exactly three ingredients and allow duplicates.

Each recipe defines:

- stable ID, localized name, category, tags, collection rarity and base sale value;
- one or more mandatory requirement clauses;
- a positive base discovery weight;
- optional ingredient-specific weight bonuses.

Required clause types for v2:

- exact ingredient with count;
- ingredient group with count;
- N distinct ingredients from a group;
- `any_of` exact ingredients where useful.

All clauses are ANDed. A recipe is eligible only when all clauses are satisfied. Its experiment score is:

`baseWeight + sum(applicable optional bonuses)`

Apply an optional bonus once when its ingredient is present, regardless of duplicate count. Selected neutral ingredients neither invalidate a recipe nor add weight. Probability is the recipe score divided by all eligible recipe scores. A selection with no eligible recipe must be rejected before consuming inventory.

The preview and final experiment claim must call the same pure outcome-calculation path so displayed and actual pools cannot diverge. The random result remains deferred until claim time. Production remains deterministic, requires discovery, and must validate that the selected three ingredients satisfy that recipe's mandatory clauses. Do not retain the old rule that every production ingredient needs a positive legacy outcome edge.

Ingredient groups and all recipe requirements/bonuses must be JSON-driven. Typical new seasonal recipes or ingredients must not require C# changes.

Epic ingredients may have at most five direct recipe associations each; legendary ingredients at most three. Generic group requirements may also accept them and do not count toward this cap.

## Recipe catalogue

Implement exactly 175 enabled recipes using `docs/recipe_catalog_v2.md` as the approved baseline. Preserve its Polish names, mandatory requirements, base weights and optional bonuses unless a mechanical inconsistency forces a minimal documented correction. Assign sensible stable IDs, English localization, categories, tags, collection rarities and base values. Balance sale values monotonically with recipe difficulty/collection rarity and avoid overflow.

Recommended broad categories: beer, grain spirit, whisky, mead, sugar spirit/rum, fruit wine/cider, fruit spirit/liqueur, gin, herbal/spiced, special. Tags are many-to-many and should carry narrower descriptors such as beer, hoppy, wheat, rye, fruit, orange, smoky, oak-aged, gin, herbal, spiced, honey, rum and prestige.

## Deliveries and economy

Update all delivery pools to contain only the 22 canonical ingredients. Preserve the intended scarcity order. Common ingredients must sustain three-ingredient experiments; rare ingredients should primarily come from deliveries and contracts; epic ingredients primarily from contracts with a small delivery chance; legendary ingredients primarily from prestige contracts with at most a very small delivery chance.

Audit delivery math rather than blindly retaining old weights. Note that the current runtime uses `freeDeliveryMinItems/freeDeliveryMaxItems` for roll count while `DeliveryPool.rolls` is currently only validated; either make one field authoritative and remove/deprecate the other safely, or make runtime honor the documented field. Cover the chosen behavior with tests.

Rebalance recipe values, laboratory costs, contract rewards, durations and ingredient inflow enough that the expanded content is testable and does not obviously deadlock. Do not redesign unrelated progression without evidence.

## Contracts v2

Keep three active contract slots, with one contract from each role:

1. `basic`: reliably attainable broad target and low/medium reward.
2. `specialist`: discovered recipe, tag, category, exact ingredient or ingredient group.
3. `prestige`: high product rarity, recipe plus rarity, epic ingredient, multiple distinct recipes, discovery or rarity-record improvement.

Contracts are generated from data-driven templates, not 175 hand-authored recipe contracts. Seed roughly 30–40 templates across the roles. Templates need stable IDs, role/tier, selection weight, amount range, target constraints, eligibility constraints and reward definition.

Supported objectives:

- produce exact recipe;
- produce category;
- produce tag;
- produce product rarity (meaning output rarity, not ingredient rarity);
- use exact ingredient;
- use ingredient group;
- discover N recipes;
- produce N distinct recipes matching a target;
- produce exact recipe at minimum rarity;
- improve a recipe rarity record;
- produce via experiment or deterministic production.

Generated targets must be attainable based on discovered recipes, accessible content, laboratory progression and realistic rarity odds. Exact-recipe production contracts should normally select discovered recipes. A discovery contract may intentionally target undiscovered content but must say so explicitly. Avoid duplicate active contracts and strongly overlapping active targets.

Contract progress must receive a full immutable production event: recipe ID, output rarity, recipe category/tags, selected ingredient IDs/groups, source (experiment/production), whether newly discovered and whether rarity record improved. Collect All and individual claim must produce identical progress. Distinct-recipe objectives must persist their seen recipe IDs.

Contract rewards should support gold plus weighted ingredient rewards by exact ingredient, group and/or rarity. Basic contracts award common ingredients, specialist contracts can award rare ingredients, and prestige contracts can award epic ingredients with a controlled legendary chance. Never generate a reward referring to removed ingredients. Preserve the existing rule that sale gold and completed-contract rewards are granted through the pending result claim flow.

Provide a safe refresh policy so an unsuitable contract cannot permanently block a slot: either a configured timed refresh or a configured free reroll. Keep it data-driven and persist any timestamps/counters required.

## Save compatibility

Increment the save version and migrate version 7 saves. Preserve gold, inventory entries whose IDs still exist, recipe progress whose IDs still exist, products, laboratory level/jobs, language and timing data. Remove or safely map deleted content IDs. Regenerate invalid legacy contracts without duplicates. Persist generated contract targets, role, progress, distinct-recipe sets and refresh state so reloading cannot reroll or lose progress.

Do not silently pay invalid old contracts or duplicate pending rewards. Pending results saved before migration must remain claimable or be migrated explicitly and safely.

## Validation

Expand `ConfigValidator` to reject at least:

- unknown/duplicate group, ingredient, recipe, category, tag/template IDs;
- ingredients referencing unknown groups or invalid rarities;
- recipes with no requirements, unsatisfiable clauses, unknown references, non-positive base weight/value or invalid optional bonuses;
- recipes that cannot be produced with exactly three selected ingredients;
- duplicate/contradictory requirements;
- epic/legendary direct-association caps being exceeded;
- contract templates with incompatible objective/target/reward fields;
- targets that cannot resolve to any enabled content;
- reward pools with invalid quantities, weights or references;
- missing PL/EN localization for every new player-facing entity;
- delivery pools missing enabled ingredients unless deliberately excluded by an explicit source rule.

Validation errors should identify the offending entity and field.

## UI and documentation

Update experiment preview to show only eligible recipes and correct normalized chances. Show mandatory exact ingredients/groups clearly in the recipe book and production selection. Continue hiding undiscovered recipe identity where the current design requires it, but do not leak it through requirement text.

Update contract UI for role, generated target, progress, distinct progress, minimum rarity, rewards and refresh/reroll. Ensure Polish and English localization is complete. Avoid a single unvirtualized wall of 175 expensive UI elements if the current prototype noticeably stalls; use practical filtering/category navigation within the existing UI architecture.

Update README, `docs/data_model.md` and `docs/design_rules.md` so documentation describes implemented behavior, not the obsolete ingredient-owned outcome-weight model.

## Required tests and verification

Add focused EditMode tests for:

- exact/group/distinct/any-of requirement eligibility;
- optional bonuses and probability normalization;
- duplicate ingredients and once-only bonus application;
- preview/claim pool equivalence and deferred random roll;
- deterministic production validation;
- all 175 recipes reachable by at least one three-ingredient multiset;
- all JSON references and localization;
- rarity direct-association caps;
- delivery rarity/source coverage;
- one contract per role, attainable generation and no duplicates;
- every new contract objective and reward selector;
- ingredient/group/source/discovery/record events;
- Collect All parity;
- contract state save/load and v7 migration;
- pending-result reward safety;
- existing timing, offline, mastery, laboratory and product-rarity tests.

Run the full Unity EditMode suite using the repository's documented command. Inspect `TestResults.xml` and report exact totals. Also run a deterministic simulation over many deliveries/experiments to detect unreachable recipes, empty outcome pools, reward deadlocks and grossly dominant recipes; keep simulation tooling out of runtime if it is only diagnostic.

## Scope discipline

Implement and verify the content/contract expansion. Do not add IAP, ads, backend services, seasons, final art or unrelated architecture. Do not commit unless explicitly asked. Preserve unrelated user changes.
