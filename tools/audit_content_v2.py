import itertools, json, random
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets/Resources/GameData"
ingredients = json.loads((DATA / "ingredients.json").read_text(encoding="utf-8"))["ingredients"]
recipes = json.loads((DATA / "recipes.json").read_text(encoding="utf-8"))["recipes"]
economy = json.loads((DATA / "economy.json").read_text(encoding="utf-8"))
templates = json.loads((DATA / "contracts.json").read_text(encoding="utf-8"))["templates"]
by_id = {x["id"]: x for x in ingredients}
ids = list(by_id)

def eligible(recipe, combo):
    counts = Counter(combo)
    for clause in recipe["requirements"]:
        n = clause.get("count", 1); typ = clause["type"]
        if typ == "ingredient": ok = counts[clause["ingredientId"]] >= n
        elif typ == "group": ok = sum(by_id[x].get("groupId") == clause["groupId"] for x in combo) >= n
        elif typ == "distinct_group": ok = len({x for x in combo if by_id[x].get("groupId") == clause["groupId"]}) >= n
        elif typ == "any_of": ok = sum(counts[x] for x in clause["ingredientIds"]) >= n
        else: ok = False
        if not ok: return False
    return True

def score(recipe, combo):
    if not eligible(recipe, combo): return 0
    selected = set(combo); groups = {by_id[x].get("groupId") for x in selected}
    return recipe["baseWeight"] + sum(b["weight"] for b in recipe.get("weightBonuses", [])
        if b.get("ingredientId") in selected or b.get("groupId") in groups)

combos = list(itertools.combinations_with_replacement(ids, 3))
reach = defaultdict(int); expected = Counter(); empty = 0; max_probability = (0, None, None); valid = 0
for combo in combos:
    pool = [(r, score(r, combo)) for r in recipes]
    pool = [(r, w) for r, w in pool if w > 0]
    if not pool:
        empty += 1; continue
    valid += 1; total = sum(w for _, w in pool)
    for recipe, weight in pool:
        reach[recipe["id"]] += 1
        p = weight / total; expected[recipe["id"]] += p / len(combos)
        if p > max_probability[0]: max_probability = (p, recipe["id"], combo)

unreachable = [r["id"] for r in recipes if not reach[r["id"]]]
dominant = expected.most_common(10)

pool = economy["deliveryPools"][0]
rng = random.Random(20260621); delivery_counts = Counter()
total_weight = sum(x["weight"] for x in pool["entries"])
for _ in range(100_000 * pool["rolls"]):
    roll = rng.randrange(total_weight)
    for entry in pool["entries"]:
        roll -= entry["weight"]
        if roll < 0:
            delivery_counts[entry["ingredientId"]] += rng.randint(entry["minAmount"], entry["maxAmount"]); break
rarity_delivery = Counter()
for iid, amount in delivery_counts.items(): rarity_delivery[by_id[iid]["rarityId"]] += amount

roles = {role: [x for x in templates if x["enabled"] and x["role"] == role] for role in ("basic", "specialist", "prestige")}
reward_rarities = {x["targetId"] for t in templates for x in t.get("ingredientRewards", []) if x["selectorType"] == "rarity"}
delivery_ids = {x["ingredientId"] for x in pool["entries"]}
contract_blockers = [role for role, values in roles.items() if not values]
source_gaps = [x for x in ids if x not in delivery_ids and by_id[x].get("sourceRule") != "contracts_only"]

print(f"ingredients={len(ingredients)} groups={len({x.get('groupId') for x in ingredients if x.get('groupId')})} recipes={len(recipes)} templates={len(templates)}")
print(f"three_slot_multisets={len(combos)} valid_pools={valid} empty_pools={empty} unreachable_recipes={len(unreachable)}")
print(f"solution_count_min={min(reach.values()) if reach else 0} solution_count_max={max(reach.values()) if reach else 0}")
print(f"max_single_pool_probability={max_probability[0]:.4f} recipe={max_probability[1]} combo={','.join(max_probability[2] or [])}")
print("aggregate_top=" + ", ".join(f"{rid}:{share:.4%}" for rid, share in dominant))
print("delivery_items_by_rarity_100k=" + json.dumps(rarity_delivery, sort_keys=True))
print(f"delivery_source_gaps={len(source_gaps)} contract_role_blockers={len(contract_blockers)} reward_rarity_coverage={sorted(reward_rarities)}")
if unreachable: print("UNREACHABLE=" + ",".join(unreachable))
if source_gaps: print("SOURCE_GAPS=" + ",".join(source_gaps))
if contract_blockers: print("CONTRACT_BLOCKERS=" + ",".join(contract_blockers))

assert len(ingredients) == 22 and len(recipes) == 175 and len(templates) == 36
assert not unreachable and not source_gaps and not contract_blockers
assert all(x["weight"] > 0 and x["minAmount"] > 0 and x["maxAmount"] >= x["minAmount"] for x in pool["entries"])
assert {"rarity_common", "rarity_rare", "rarity_epic", "rarity_legendary"} <= set(rarity_delivery)
assert {"rarity_common", "rarity_rare", "rarity_epic", "rarity_legendary"} <= reward_rarities
