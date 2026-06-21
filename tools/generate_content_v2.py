import json, re, unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets/Resources/GameData"

ingredients = [
    ("barley", "common", "grain", "Jęczmień", "Barley"), ("wheat", "common", "grain", "Pszenica", "Wheat"),
    ("rye", "common", "grain", "Żyto", "Rye"), ("honey", "common", "sweetener", "Miód", "Honey"),
    ("cane_sugar", "common", "sweetener", "Cukier trzcinowy", "Cane Sugar"), ("molasses", "common", "sweetener", "Melasa", "Molasses"),
    ("plum", "common", "fruit", "Śliwka", "Plum"), ("apple", "common", "fruit", "Jabłko", "Apple"),
    ("grape", "common", "fruit", "Winogrona", "Grapes"), ("orange", "common", "fruit", "Pomarańcza", "Orange"),
    ("juniper", "rare", "herb", "Jałowiec", "Juniper"), ("wormwood", "rare", "herb", "Piołun", "Wormwood"),
    ("anise", "rare", "spice", "Anyż", "Anise"), ("pepper", "rare", "spice", "Pieprz", "Pepper"),
    ("hops", "rare", None, "Chmiel", "Hops"), ("peat", "rare", None, "Torf", "Peat"),
    ("oak", "rare", None, "Dębina", "Oak"), ("angelica", "epic", "herb", "Arcydzięgiel", "Angelica"),
    ("vanilla", "epic", "spice", "Wanilia", "Vanilla"), ("tonka", "epic", "spice", "Tonka", "Tonka Bean"),
    ("amber", "legendary", None, "Bursztyn", "Amber"), ("mastic", "legendary", None, "Mastyks", "Mastic")
]
ingredient_ids = {base: f"ingredient_{base}_{rarity}" for base, rarity, *_ in ingredients}

group_names = {
    "grain": ("Zboża", "Grains"), "sweetener": ("Słodziwa", "Sweeteners"), "fruit": ("Owoce", "Fruit"),
    "herb": ("Zioła", "Herbs"), "spice": ("Przyprawy", "Spices")
}
group_tokens = {"G": "grain", "S": "sweetener", "F": "fruit", "H": "herb", "P": "spice"}

english_names = """
Table Beer
Barley Beer
Wheat Beer
Rye Beer
Pale Ale
Bitter Ale
Amber Ale
Red Ale
Brown Ale
Mild Ale
Strong Ale
IPA
Double IPA
Wheat IPA
Rye IPA
Porter
Baltic Porter
Stout
Honey Stout
Peated Stout
Fruit Beer
Plum Ale
Apple Ale
Grape Ale
Orange Wheat Beer
Herbal Saison
Spiced Saison
Braggot
Rye Kvass
Oak Ale
Grain Wash
Barley Spirit
Wheat Spirit
Rye Spirit
Multi-Grain Spirit
Young Malt Whisky
Wheat Whisky
Rye Whisky
Blended Whisky
Malt Whisky
Double Oak Whisky
Honey Whisky
Grape Whisky
Orange Whisky
Peated Whisky
Heavy Peated Whisky
Peated Rye Whisky
Smoked Wheat Whisky
Grain Vodka
Wheat Vodka
Rye Vodka
Honey Vodka
Pepper Vodka
Orange Vodka
Plum Vodka
Grain Anise Spirit
Oak Vodka
Herbal Grain Spirit
Spiced Grain Spirit
Aged Grain Spirit
Mead
Dry Mead
Sweet Mead
Heavy Mead
Apple Mead
Plum Mead
Grape Mead
Orange Mead
Hopped Mead
Juniper Mead
Wormwood Mead
Spiced Mead
Anise Mead
Pepper Mead
Oak Mead
Peated Mead
Cane Wine
Molasses Wine
Light Rum
Dark Rum
Oak Rum
Spiced Rum
Orange Rum
Honey Rum
Peated Rum
Fruit Wine
Apple Cider
Dry Cider
Sweet Cider
Hopped Cider
Spiced Cider
Oak Cider
Plum Wine
Sweet Plum Wine
Spiced Plum Wine
Smoked Plum Wine
Grape Wine
Dry Grape Wine
Dessert Wine
Honey Wine
Herbal Vermouth
Spiced Vermouth
Oak Wine
Grape-Orange Wine
Orange Ferment
Bitter Orange Wine
Honey-Orange Wine
Orchard Wine
Fruit Cuvée
Fortified Fruit Wine
Fruit Spirit
Plum Brandy
Aged Plum Brandy
Peated Plum Brandy
Plum-Honey Liqueur
Spiced Plum Liqueur
Apple Spirit
Aged Apple Spirit
Apple-Honey Liqueur
Peppered Apple Spirit
Grape Brandy
Aged Brandy
Grape-Honey Liqueur
Herbal Pomace Brandy
Orange Spirit
Orange Liqueur
Bitter Orange Liqueur
Orange-Anise Liqueur
Fruit Ratafia
Plum-Apple Brandy
Grape-Orange Aperitif
Orchard Spirit
Grain-Fruit Spirit
Honey Fruit Cordial
Smoked Fruit Brandy
Grain Gin
Dry Gin
Sweetened Gin
Orange Gin
Plum Gin
Grape Gin
Honey Gin
Hopped Gin
Oak Gin
Peated Gin
Wormwood Spirit
Absinthe
Honey Absinthe
Orange Bitter
Herbal Bitter
Pepper Bitter
Herbal Liqueur
Grain Herbal Liqueur
Fruit Herbal Liqueur
Anise Liqueur
Anise Spirit
Pepper Liqueur
Pepper Spirit
Spiced Liqueur
Spiced Fruit Cordial
Vanilla Liqueur
Vanilla Mead
Vanilla-Orange Cordial
Tonka Liqueur
Dark Tonka Rum
Oak Tonka Spirit
Angelica Gin
Angelica Liqueur
Angelica Vermouth
Amber Tincture
Amber Mead
Amber Oak Spirit
Mastic Liqueur
Grape Mastic Spirit
Mastic-Orange Cordial
""".strip().splitlines()
assert len(english_names) == 175

def slug(s):
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode().lower()
    return re.sub(r"[^a-z0-9]+", "_", s).strip("_")

catalog = (ROOT / "docs/recipe_catalog_v2.md").read_text(encoding="utf-8")
lines = [m.groups() for m in re.finditer(r"^(\d+)\. (.+?) — (.+?); W(\d+)(?:; (.*))?$", catalog, re.M)]
assert len(lines) == 175, len(lines)

category_ranges = [
    (1, 30, "beer", "Piwa", "Beer"), (31, 60, "grain_spirit", "Destylaty zbożowe", "Grain Spirits"),
    (61, 85, "mead_sugar", "Miody i destylaty cukrowe", "Mead & Sugar Spirits"),
    (86, 110, "fruit_wine", "Wina owocowe i cydry", "Fruit Wines & Ciders"),
    (111, 135, "fruit_spirit", "Destylaty i likiery owocowe", "Fruit Spirits & Liqueurs"),
    (136, 160, "gin_herbal", "Giny i trunki ziołowe", "Gin, Anise & Herbal"),
    (161, 175, "special", "Trunki specjalne", "Special")
]

def category_for(n):
    return next(x for a, b, *x in category_ranges if a <= n <= b)

def parse_selector(token, distinct=False):
    token = token.strip()
    if token in group_tokens:
        return {"type": "distinct_group" if distinct else "group", "groupId": group_tokens[token], "count": 2 if distinct else 1}
    base = token.replace("cane sugar", "cane_sugar")
    assert base in ingredient_ids, token
    return {"type": "ingredient", "ingredientId": ingredient_ids[base], "count": 1}

recipes = []
localization = []
for i, (number, pl_name, req_text, weight, bonus_text) in enumerate(lines):
    n = int(number); en_name = english_names[i]; category_id, _, _ = category_for(n)
    requirements = []
    for raw in [x.strip() for x in req_text.split("&")]:
        m = re.fullmatch(r"2 distinct ([GSFHP])", raw)
        requirements.append(parse_selector(m.group(1), True) if m else parse_selector(raw))
    bonuses = []
    if bonus_text:
        for raw in bonus_text.split(","):
            target, amount = raw.strip().rsplit("+", 1)
            if target in group_tokens: bonuses.append({"groupId": group_tokens[target], "weight": int(amount)})
            else: bonuses.append({"ingredientId": ingredient_ids[target.replace("cane sugar", "cane_sugar")], "weight": int(amount)})
    direct_ranks = []
    for r in requirements:
        if r["type"] == "ingredient": direct_ranks.append(next(x[1] for x in ingredients if ingredient_ids[x[0]] == r["ingredientId"]))
    max_rank = max(({"common": 1, "rare": 2, "epic": 3, "legendary": 4}[x] for x in direct_ranks), default=1)
    complexity = len(requirements) + sum(1 for r in requirements if r["type"] == "distinct_group")
    difficulty = (110 - int(weight)) + complexity * 18 + max_rank * 20
    rank = 4 if max_rank == 4 else 3 if max_rank == 3 or int(weight) <= 10 else 2 if complexity >= 2 or int(weight) < 35 or max_rank == 2 else 1
    rarity = [None, "rarity_common", "rarity_rare", "rarity_epic", "rarity_legendary"][rank]
    bands = {1: (24, 68), 2: (90, 168), 3: (240, 355), 4: (520, 690)}
    low, high = bands[rank]; base_value = low + min(high - low, max(0, difficulty) * (high - low) // 400)
    rid = f"recipe_{n:03d}_{slug(en_name)}"
    tags = {category_id.replace("_", "-")}
    if category_id == "beer": tags.add("beer")
    if "Whisky" in en_name: tags.add("whisky")
    if "Mead" in en_name or en_name == "Braggot": tags.add("mead")
    if "Rum" in en_name: tags.add("rum")
    if "Gin" in en_name: tags.add("gin")
    if any(x in en_name for x in ("Wine", "Cider", "Fruit", "Plum", "Apple", "Grape", "Orange")): tags.add("fruit")
    if any(x in en_name for x in ("Herbal", "Wormwood", "Vermouth", "Absinthe")): tags.add("herbal")
    if any(x in en_name for x in ("Spiced", "Pepper", "Anise", "Tonka", "Vanilla")): tags.add("spiced")
    all_selectors = requirements + bonuses
    for x in all_selectors:
        if x.get("groupId"): tags.add(x["groupId"])
        if x.get("ingredientId"):
            base = next(k for k, v in ingredient_ids.items() if v == x["ingredientId"]); tags.add(base.replace("_", "-"))
    if "oak" in tags: tags.add("oak-aged")
    if "peat" in tags: tags.add("smoky")
    if "hops" in tags: tags.add("hoppy")
    if rank >= 3: tags.add("prestige")
    recipes.append({"id": rid, "displayName": en_name, "categoryId": category_id, "tags": sorted(tags),
                    "baseValue": base_value, "collectionRarityId": rarity, "baseWeight": int(weight),
                    "requirements": requirements, "weightBonuses": bonuses, "enabled": True})
    localization.append({"key": f"recipe.{rid}", "pl": pl_name, "en": en_name})

ingredient_json = []
quality = {"common": .01, "rare": .08, "epic": .16, "legendary": .24}
for base, rarity, group, pl, en in ingredients:
    row = {"id": ingredient_ids[base], "baseIngredientId": base, "displayName": en, "rarityId": f"rarity_{rarity}",
           "sourceRule": "delivery", "qualityBonus": quality[rarity], "enabled": True}
    if group: row["groupId"] = group
    ingredient_json.append(row)
    localization.append({"key": f"ingredient.{row['id']}", "pl": pl, "en": en})

groups_json = [{"id": gid, "displayName": en} for gid, (pl, en) in group_names.items()]
localization += [{"key": f"group.{gid}", "pl": pl, "en": en} for gid, (pl, en) in group_names.items()]
categories_json = [{"id": cid, "displayName": en} for _, _, cid, pl, en in category_ranges]
localization += [{"key": f"category.{cid}", "pl": pl, "en": en} for _, _, cid, pl, en in category_ranges]
tag_pl = {
    "amber":"bursztynowy", "angelica":"arcydzięglowy", "anise":"anyżowy", "apple":"jabłkowy", "barley":"jęczmienny",
    "beer":"piwo", "cane-sugar":"trzcinowy", "fruit":"owocowy", "fruit-spirit":"destylat owocowy", "fruit-wine":"wino owocowe",
    "gin":"gin", "gin-herbal":"gin i zioła", "grain":"zbożowy", "grain-spirit":"destylat zbożowy", "grape":"winogronowy",
    "herb":"zioła", "herbal":"ziołowy", "honey":"miodowy", "hoppy":"chmielony", "hops":"chmiel", "juniper":"jałowiec",
    "mastic":"mastyksowy", "mead":"miód pitny", "mead-sugar":"miód i cukier", "molasses":"melasowy", "oak":"dębowy",
    "oak-aged":"starzony w dębie", "orange":"pomarańczowy", "peat":"torf", "pepper":"pieprzny", "plum":"śliwkowy",
    "prestige":"prestiżowy", "rum":"rum", "rye":"żytni", "smoky":"dymny", "special":"specjalny", "spice":"przyprawy",
    "spiced":"korzenny", "sweetener":"słodzony", "tonka":"tonka", "vanilla":"waniliowy", "wheat":"pszeniczny",
    "whisky":"whisky", "wormwood":"piołun"
}
all_tags = sorted({tag for recipe in recipes for tag in recipe["tags"]})
assert set(all_tags) == set(tag_pl)
localization += [{"key": f"tag.{tag}", "pl": tag_pl[tag], "en": tag.replace("-", " ").title()} for tag in all_tags]

def reward(role):
    if role == "basic": return [{"selectorType": "rarity", "targetId": "rarity_common", "weight": 5, "minAmount": 2, "maxAmount": 4},
                                {"selectorType": "group", "targetId": "grain", "weight": 2, "minAmount": 2, "maxAmount": 3}]
    if role == "specialist": return [{"selectorType": "rarity", "targetId": "rarity_rare", "weight": 4, "minAmount": 1, "maxAmount": 2},
                                     {"selectorType": "group", "targetId": "herb", "weight": 2, "minAmount": 1, "maxAmount": 2},
                                     {"selectorType": "ingredient", "targetId": ingredient_ids["hops"], "weight": 1, "minAmount": 1, "maxAmount": 2}]
    return [{"selectorType": "rarity", "targetId": "rarity_epic", "weight": 18, "minAmount": 1, "maxAmount": 1},
            {"selectorType": "rarity", "targetId": "rarity_legendary", "weight": 1, "minAmount": 1, "maxAmount": 1}]

templates = []
contract_loc = []
def add(role, code, objective, selector="none", fixed=None, allowed=None, amounts=(1, 2), gold=(80, 120), min_rarity=None, source=None, lab=1, pl=None, en=None):
    tid = f"contract_{role}_{code}"
    row = {"id": tid, "displayName": en, "role": role, "tier": {"basic":1,"specialist":2,"prestige":3}[role],
           "selectionWeight": 10, "objectiveType": objective, "targetSelector": selector,
           "minAmount": amounts[0], "maxAmount": amounts[1], "minLaboratoryLevel": lab, "enabled": True,
           "minGoldReward": gold[0], "maxGoldReward": gold[1], "ingredientRewards": reward(role)}
    if fixed is not None: row["fixedTargetId"] = fixed
    if allowed: row["allowedTargetIds"] = allowed
    if min_rarity: row["minRarityId"] = min_rarity
    if source: row["source"] = source
    templates.append(row); contract_loc.append({"key": f"contract.{tid}", "pl": pl, "en": en})

# 12 basic: broad, always attainable.
add("basic","category_a","produce_category","category",amounts=(2,3),pl="Zamówienie regionalne",en="Regional Order")
add("basic","category_b","produce_category","category",amounts=(1,2),pl="Mała partia",en="Small Batch")
add("basic","tag_a","produce_tag","tag",amounts=(2,3),pl="Popularny styl",en="Popular Style")
add("basic","tag_b","produce_tag","tag",amounts=(1,2),pl="Próba smaku",en="Taste Trial")
add("basic","source_experiment","produce_source","source",fixed="experiment",amounts=(2,3),pl="Praca badawcza",en="Research Work")
add("basic","source_production","produce_source","source",fixed="production",amounts=(1,2),pl="Pewna dostawa",en="Reliable Supply")
add("basic","common","produce_rarity","rarity",fixed="rarity_common",amounts=(2,3),pl="Codzienna jakość",en="Everyday Quality")
add("basic","rare","produce_rarity","rarity",fixed="rarity_rare",amounts=(1,1),pl="Lepsza partia",en="Better Batch")
add("basic","group_a","use_group","group",amounts=(2,3),pl="Podstawowe surowce",en="Core Materials")
add("basic","group_b","use_group","group",amounts=(1,2),pl="Dobór składników",en="Ingredient Choice")
add("basic","ingredient_a","use_ingredient","ingredient",amounts=(2,3),pl="Stały składnik",en="Staple Ingredient")
add("basic","discover","discover_recipes","none",amounts=(1,1),gold=(100,140),pl="Pierwsze odkrycie",en="First Discovery")

# 12 specialist templates.
add("specialist","recipe_a","produce_recipe","discovered_recipe",amounts=(2,3),gold=(180,260),pl="Specjalność zakładu",en="House Specialty")
add("specialist","recipe_b","produce_recipe","discovered_recipe",amounts=(1,2),gold=(160,230),pl="Powtórka mistrza",en="Master's Repeat")
add("specialist","category_a","produce_category","category",amounts=(3,5),gold=(170,250),pl="Kontrakt branżowy",en="Trade Contract")
add("specialist","category_b","produce_category","category",amounts=(2,4),gold=(160,240),pl="Wybrana kategoria",en="Selected Category")
add("specialist","tag_a","produce_tag","tag",amounts=(3,4),gold=(180,260),pl="Profil smakowy",en="Flavor Profile")
add("specialist","tag_b","produce_tag","tag",amounts=(2,3),gold=(170,250),pl="Styl destylarni",en="Distillery Style")
add("specialist","ingredient_a","use_ingredient","ingredient",amounts=(2,4),gold=(190,270),pl="Ekspertyza surowcowa",en="Ingredient Expertise")
add("specialist","ingredient_b","use_ingredient","ingredient",amounts=(1,3),gold=(180,260),pl="Wybrany surowiec",en="Chosen Material")
add("specialist","group_a","use_group","group",amounts=(3,5),gold=(180,260),pl="Rodzina składników",en="Ingredient Family")
add("specialist","group_b","use_group","group",amounts=(2,4),gold=(170,250),pl="Specjalizacja grupowa",en="Group Specialty")
add("specialist","experiment","produce_source","source",fixed="experiment",amounts=(3,5),gold=(180,250),pl="Seria eksperymentów",en="Experiment Series")
add("specialist","production","produce_source","source",fixed="production",amounts=(3,5),gold=(180,250),pl="Seria produkcyjna",en="Production Run")

# 12 prestige templates.
add("prestige","epic","produce_rarity","rarity",fixed="rarity_epic",amounts=(1,2),gold=(420,600),lab=2,pl="Epicka jakość",en="Epic Quality")
add("prestige","legendary","produce_rarity","rarity",fixed="rarity_legendary",amounts=(1,1),gold=(650,850),lab=3,pl="Legendarne zamówienie",en="Legendary Order")
add("prestige","recipe_rare","recipe_min_rarity","discovered_recipe",amounts=(1,2),gold=(450,650),min_rarity="rarity_rare",lab=2,pl="Receptura wyborna",en="Fine Recipe")
add("prestige","recipe_epic","recipe_min_rarity","discovered_recipe",amounts=(1,1),gold=(550,750),min_rarity="rarity_epic",lab=2,pl="Receptura epicka",en="Epic Recipe")
add("prestige","distinct_a","distinct_recipes","category",amounts=(3,5),gold=(430,620),lab=1,pl="Kolekcja kategorii",en="Category Collection")
add("prestige","distinct_b","distinct_recipes","tag",amounts=(3,4),gold=(430,620),lab=2,pl="Kolekcja stylu",en="Style Collection")
add("prestige","discover_a","discover_recipes","none",amounts=(2,3),gold=(480,680),pl="Wielkie odkrycia",en="Great Discoveries")
add("prestige","discover_target","discover_recipes","undiscovered_recipe",amounts=(1,1),gold=(520,720),pl="Poszukiwana receptura",en="Sought Recipe")
add("prestige","record_a","improve_record","discovered_recipe",amounts=(1,2),gold=(460,650),lab=2,pl="Pobicie rekordu",en="Record Breaker")
add("prestige","record_b","improve_record","none",amounts=(2,3),gold=(500,700),lab=2,pl="Seria rekordów",en="Record Streak")
add("prestige","epic_ingredient","use_ingredient","ingredient",allowed=[ingredient_ids[x] for x in ("angelica","vanilla","tonka")],amounts=(1,2),gold=(480,680),lab=2,pl="Epicki surowiec",en="Epic Material")
add("prestige","prestige_tag","produce_tag","tag",fixed="prestige",amounts=(2,3),gold=(480,680),lab=2,pl="Prestiżowa partia",en="Prestige Batch")
assert len(templates) == 36

weights = {"common": 0, "rare": 0, "epic": 0, "legendary": 0}
delivery_entries = []
for base, rarity, *_ in ingredients:
    idx = weights[rarity]; weights[rarity] += 1
    base_weight = {"common": 100, "rare": 24, "epic": 3, "legendary": 1}[rarity]
    delivery_entries.append({"ingredientId": ingredient_ids[base], "weight": max(1, base_weight - idx), "minAmount": 1,
                             "maxAmount": 2 if rarity == "common" else 1})

economy = {
    "startingGold": 150, "ingredientsPerExperiment": 3, "ingredientsPerProduction": 3, "activeContractCount": 3,
    "freeDeliveryIntervalSeconds": 7200, "experimentDurationSeconds": 3600, "productionDurationSeconds": 1800,
    "maxStoredFreeDeliveries": 3, "maxOfflineProgressSeconds": 0, "freeContractRerolls": 1, "contractRefreshSeconds": 86400,
    "ingredientQualityInfluence": 1.0, "laboratoryQualityInfluence": 1.0,
    "productRarityWeights": [{"rarityId":"rarity_common","weight":7000},{"rarityId":"rarity_rare","weight":2200},
                             {"rarityId":"rarity_epic","weight":700},{"rarityId":"rarity_legendary","weight":95},{"rarityId":"rarity_mythic","weight":5}],
    "deliveryPools": [{"id": "pool_base", "rolls": 8, "entries": delivery_entries}]
}

old_loc = json.loads((DATA / "localization.json").read_text(encoding="utf-8"))["entries"]
keep_prefixes = ("ui.", "rarity.", "mastery.")
base_loc = [x for x in old_loc if x["key"].startswith(keep_prefixes)]
for entry in base_loc:
    if entry["key"] == "ui.production.help":
        entry["pl"] = "Wybierz dokładnie 3 składniki spełniające wymagania receptury. Składniki neutralne i duplikaty są dozwolone."
        entry["en"] = "Choose exactly 3 ingredients satisfying the recipe requirements. Neutral ingredients and duplicates are allowed."
all_loc = base_loc + localization + contract_loc

def write(name, value):
    (DATA / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

write("ingredient_groups.json", {"groups": groups_json})
write("ingredients.json", {"ingredients": ingredient_json})
write("categories.json", {"categories": categories_json})
write("recipes.json", {"recipes": recipes})
write("contracts.json", {"templates": templates})
write("economy.json", economy)
write("localization.json", {"entries": all_loc})

print(f"generated ingredients={len(ingredient_json)} groups={len(groups_json)} recipes={len(recipes)} templates={len(templates)} localization={len(all_loc)}")
