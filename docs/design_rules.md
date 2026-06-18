# Distillery Discovery - MVP Design Document

## Vision

Mobile idle collection game focused on discovering recipes through ingredient experiments.

The player runs a distillery laboratory. By combining ingredients they discover new recipes, improve recipe collections, fulfill contracts, earn gold and expand their laboratories.

The core gameplay loop is based on:

* collecting ingredients,
* performing experiments,
* discovering recipes,
* improving collection completion,
* producing known products,
* upgrading laboratories.

The game must be fully data-driven so that future reskins can replace the theme without changing gameplay.

---

# Core Gameplay Loop

1. Receive ingredient deliveries.
2. Select ingredients for an experiment.
3. Review predicted outcomes.
4. Start experiment.
5. Wait or accelerate completion.
6. Receive result.
7. Discover a recipe or improve its collection rarity.
8. Sell products or fulfill contracts.
9. Upgrade laboratories.
10. Repeat.

---

# Resources

## Ingredients

Primary gameplay resource.

Ingredients are consumed when used.

Examples:

* Malt
* Hops
* Honey
* Plum
* Amber
* Juniper
* Ghost Mushroom

Ingredients have rarity levels.

Examples:

* Common
* Rare
* Epic
* Legendary

Higher rarity ingredients improve the quality of produced items and may increase discovery chances.

---

## Gold

Main soft currency.

Obtained from:

* product sales,
* contracts,
* achievements,
* events.

Used for:

* laboratory upgrades,
* laboratory construction,
* premium deliveries,
* production acceleration.

---

## Premium Currency

Main hard currency.

Obtained from:

* purchases,
* events,
* contracts,
* achievements.

Used for:

* instant delivery,
* additional ingredient selection,
* acceleration,
* premium pass.

---

# Ingredients

Each ingredient contributes weighted chances toward one or more products.

Example:

Malt:

* Beer +20
* Whisky +20

Hops:

* Beer +10

Honey:

* Mead +30

Experiment:

Malt + Hops + Honey

Results:

* Beer weight 30
* Whisky weight 20
* Mead weight 30

The final product is selected through weighted randomization.

All weights must be configurable through data.

---

# Experiment System

Experiments are used to discover recipes.

Player selects ingredients from inventory.

Ingredients are consumed.

The game calculates possible outcomes.

The player can preview:

* likely products,
* chance of undiscovered recipes,
* rarity potential.

Experiments may be:

* instant (limited daily amount),
* timed.

Timed experiments occupy laboratory slots.

Experiment results:

* discovered recipe,
* duplicate recipe,
* rarity improvement,
* product creation.

---

# Recipe Book

The recipe book is the primary collection screen.

Each recipe contains:

* icon,
* name,
* rarity category,
* discovered ingredients,
* highest obtained product rarity,
* mastery progress.

Undiscovered recipes appear as placeholders.

The player sees:

* collection progress,
* missing recipes,
* rarity achievements.

Recipe ingredients become permanently visible after first discovery.

---

# Product Rarity

Each product has a rarity level.

Examples:

* Common
* Rare
* Epic
* Legendary
* Mythic

Rarity affects:

* sale value,
* contract usefulness,
* collection status.

The recipe book stores the highest rarity ever obtained for each recipe.

Example:

Royal Mead:

Highest rarity achieved:
Epic

Future Epic or lower results do not change collection status.

Legendary upgrades collection status.

---

# Production System

Production uses already discovered recipes.

Player selects:

* known recipe,
* valid ingredients.

Unlike experiments:

* outcome is guaranteed,
* recipe is predetermined.

Ingredients are consumed.

Produced items are automatically sold or stored for contracts.

Product rarity is affected by:

* ingredient rarity,
* laboratory bonuses,
* recipe mastery bonuses.

---

# Laboratory System

Laboratories are the main progression mechanic.

Laboratories contain slots.

Slots can perform:

* experiments,
* production.

Laboratories can be upgraded.

Possible bonuses:

* reduced experiment time,
* reduced production time,
* increased rarity chance,
* increased discovery chance,
* additional slots.

Players may own multiple laboratories.

---

# Deliveries

Ingredients enter the economy through deliveries.

Base delivery:

* arrives periodically,
* contains random ingredients.

Ingredient rarity and frequency are configurable.

Additional delivery types:

* purchased with gold,
* premium deliveries,
* event deliveries.

Daily reward:

Player may select one specific ingredient per day.

Additional selections may be obtained through premium currency.

---

# Contracts

Contracts provide long-term goals.

Types:

* daily,
* weekly,
* seasonal.

Examples:

Deliver:

* 3 Mead products

Deliver:

* 1 Epic product

Produce:

* 10 products using Honey

Discover:

* 2 new recipes

Rewards:

* gold,
* ingredients,
* premium currency,
* event rewards.

---

# Recipe Mastery

Recipes gain mastery through repeated production.

Mastery progression is separate from recipe discovery.

Examples:

Level 1:
No bonus

Level 2:
+2% rarity chance

Level 5:
+5% rarity chance

Level 10:
+10% rarity chance

All values are data-driven.

---

# Seasonal Content

New seasons introduce:

* ingredients,
* recipes,
* contracts,
* cosmetic content.

The system must support adding content through configuration only.

No code changes should be required for typical seasonal updates.

---

# Data-Driven Requirement

All gameplay content must be configurable.

Configurable entities:

* ingredients,
* ingredient rarities,
* recipes,
* recipe weights,
* product rarities,
* deliveries,
* contracts,
* laboratory upgrades,
* mastery levels,
* seasonal content,
* monetization values.

The game engine should be reusable with different themes by replacing only content data and visual assets.
