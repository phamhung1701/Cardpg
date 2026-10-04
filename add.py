import openpyxl,copy
p='CardPG_GameData_organized.xlsx'; w=openpyxl.load_workbook(p); s=w['Artifacts']; H=[c.value for c in s[1]]
# concise approved additions; all are proposals, not implemented
spec=[
('Heart Emblem','Epic',3,'rel_026','Keep Rare healing/Gold; overhealing builds Vitality spent for bonus damage on next Heart attack.','TBD','Conversion/cap TBD','Heart Build'),
('Spade Emblem','Rare',2,'rel_004','Keep doubled block; fully blocking with one Spade draws 1 card.','1 card','One Spade','Spade Build'),
('Diamond Emblem','Epic',3,'rel_027','Keep Rare draw 2; every third manually played Diamond chooses a card from discard to draw.','1 card','Manual plays only','Diamond Build'),
('Hands Emblem','Epic',3,'rel_020','Keep Rare; allow four matching ranks; each card on-play effect triggers twice.','2 triggers','No replay/recursive bonus turns','Poker / Rank Build'),
('Overflow Emblem','Epic',3,'rel_021','Keep Rare; auto-play overflow cards for bonus damage equal to current hand count.','Hand-count damage','Keep existing budget','Draw Build'),
('Retaliation Emblem','Epic',3,'rel_022','Keep Rare; counterattack kill permanently increases counterattack damage.','TBD','Amount TBD','Block Build'),
('Dwarf Emblem','Rare',2,'rel_013','Keep Common bonus action; played rank <5 strengthens next higher-rank attack.','TBD','Amount TBD','Low-Rank Build'),
('Ace Emblem','Rare',2,'rel_014','Keep Common crit chance; critical Ace-pair returns Ace to hand once per encounter.','1 Ace','Once/encounter','Critical Build'),
('Mimic Emblem','Epic',2,'rel_017','Keep base; held matching-rank cards trigger in-hand effects one additional time.','+1 trigger','TBD','In-Hand Build'),
('Scavenger\'s Pouch','TBD',1,'-','First consumable used each encounter draws 1.','1 card','First/encounter','Consumable Build'),
('Balancer\'s Scale','TBD',1,'-','Alternate manually playing low-rank and Face cards to empower attacks; repeating a group resets bonus.','TBD','Low-rank threshold, bonus/reset timing TBD','Rank Build'),
('Hunter\'s Ledger','TBD',1,'-','Elite kill permanently strengthens first attack each encounter.','TBD','Amount TBD','Elite Build'),
('Traveler\'s Pack','Common',1,'-','Add 2 backpack slots (3→5).','+2 slots','TBD stacking','Consumable Build'),
('Traveler\'s Pack','Rare',2,'rel_105','Retain 5 slots; up to 3 identical consumables per slot. Track charges/item, use partially-used first, separate rune suits; capacity loss withdraw-only, never delete.','3/slot','Capacity loss withdraw-only','Consumable Build'),
('Field Medic\'s Kit','TBD',1,'-','First consumable per encounter also heals.','TBD','Amount TBD','Healing Build'),
('Preparation Manual','TBD',1,'-','Starting block per distinct consumable type in backpack.','TBD','Amount TBD','Consumable Build'),
('Merchant\'s Badge','TBD',1,'-','20% purchase discount.','20%','Before cashback; provisional','Economy Build'),
('Cashback Token','TBD',1,'-','Refund 15% of actual Gold spent; no free-item cashback, no refund beyond cost, no retroactive own benefit.','15%','Provisional','Economy Build'),
('Bounty Ledger','TBD',1,'-','Elites +50% Gold; Bosses +25% Gold.','+50% / +25%','Rounding TBD; provisional','Economy Build'),
('Golden Vault','TBD',1,'-','Boss defeat grants 10% of unspent Gold.','10%','Infinite savings role; provisional','Economy Build'),
('Gilded Blade','TBD',1,'-','Opening attack bonus 1 per 10 Gold held.','+1/10 Gold','Rounding TBD; provisional','Economy Build'),
('Scavenger\'s Satchel','TBD',1,'-','Every 3 combat wins: random non-enhancement consumable.','1','Full tray replace/decline','Consumable Build'),
('Alchemist\'s Kit','TBD',1,'-','At new map choose 1 of 3 random enhancement consumables.','1 of 3','Full tray replace/decline; not enemy drops','Enhancement Build'),
('Merchant\'s Gift','TBD',1,'-','After 3 item purchases: random non-enhancement consumable.','1','Free rewards do not count; full tray replace/decline','Economy Build'),
('Surveyor\'s Map','TBD',1,'-','At map start reveal all Shops/Elites; no connectivity changes.','All Shops/Elites','No new connectivity','Map Build'),
('Wanderer\'s Boots','TBD',1,'-','Visit 3 different node types in map: heal + Gold once/map.','TBD','Amounts TBD','Map Build'),
('Warpath Banner','TBD',1,'-','Consecutive combat wins increase starting block next combat.','TBD','Noncombat/map reset; amount/cap TBD','Map Build'),
('Hidden Trail','TBD',1,'-','Once/map, before entry reroll reachable hidden node to different hidden type.','1 reroll','No Boss change/new nodes/route bypass','Map Build'),
('Challenger\'s Crest','TBD',1,'-','Start Elite/Boss with 1 Shield.','1 Shield','TBD','Elite / Boss Build'),
('Trophy Rack','TBD',1,'-','Elite kill permanently strengthens opening attack vs Boss.','TBD','Amount TBD','Elite / Boss Build'),
('Kingslayer\'s Mark','TBD',1,'-','Vs Boss, play 3 different ranks to empower next attack; consume charge and repeat.','TBD','Per-card/manual trigger unresolved','Boss Build'),
('Crown of Endurance','TBD',1,'-','Boss kill permanently increases max HP; additional if encounter has zero-HP loss.','TBD','Amounts TBD; shield/block not HP loss; once/defeated Boss','Boss Build'),
]
# find unused IDs (do not consume IDs assigned to existing reserved blank records)
used={s.cell(r,1).value for r in range(2,s.max_row+1) if s.cell(r,1).value}; nextid=105
for name,rarity,tier,up,effect,val,limit,synergy in spec:
 while f'rel_{nextid:03}' in used: nextid+=1
 rid=f'rel_{nextid:03}'; nextid+=1; used.add(rid)
 # compute predecessor by family/tier, including rows just added
 if up=='rel_105': up='rel_105' # common Traveler's Pack added immediately before
 if up not in ('-',) and up.startswith('rel_') and up not in used: up='TBD predecessor ID'
 data={'ID':rid,'Name':name,'Rarity':rarity,'Trigger':'See Effect','Condition':'See Effect','Effect':effect,'Value':val,'Limit':limit,'Synergy':synergy,'Intent':synergy,'Status':'Dr
