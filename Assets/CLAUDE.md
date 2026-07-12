# 🌀 CLAUDE.md — Astraleum TCG

> Fichier de contexte pour Claude. À placer à la racine du projet Unity.
> Notion de référence : https://www.notion.so/330335fc949381bfbcb0d11d221d4464

---

## 🎮 Projet

Jeu de cartes compétitif PC, **Unity 6000.4.0f1**, distribution **Steam**.
- 2 joueurs, 5 cartes fixes par joueur
- Condition de victoire : détruire toutes les cartes adverses
- 2 actions par tour, fin de tour **manuelle uniquement**
- Timer 120s par tour (auto-switch si dépassé)

---

## 📁 Structure des scripts

```
Assets/_Game/
├── Combat/Scripts/
│   ├── StackManager.cs
│   ├── PassiveManager.cs
│   ├── SkillExecutor.cs
│   ├── DamageCalculator.cs
│   ├── CombatManager.cs
│   └── TurnManager.cs
├── Cards/Scripts/
│   ├── CardInstance.cs
│   ├── CardData.cs
│   ├── CardSkill.cs
│   ├── SkillBranch.cs                ← ConditionalBranch + SkillCondition + enums branche
│   ├── CardVisualUpdater.cs
│   ├── CardClickHandler.cs
│   ├── CardHoverHandler.cs
│   ├── CardTargetHighlight.cs
│   ├── CardZoomHandler.cs
│   ├── CombatPopupHandler.cs
│   └── DeckManager.cs
├── Cards/Editor/
│   └── ConditionalBranchDrawer.cs   ← PropertyDrawer contextuel pour ConditionalBranch
├── UI/Scripts/
│   ├── CombatUIManager.cs
│   ├── StackDisplayManager.cs
│   ├── IconLineManager.cs
│   ├── StackIconHover.cs
│   ├── BtnFinTourGlow.cs
│   ├── PassiveTooltipManager.cs
│   ├── BuffTooltipManager.cs
│   ├── TurnAudioManager.cs
│   ├── TurnAnnouncementManager.cs    ← Canvas/TurnAnnouncement (scène Combat)
│   ├── DeckEditorSkillPreview.cs     ← clic droit carte dans Panel_DeckEditor (scène MainMenu)
│   ├── CollectionManager.cs          ← Panel_MyCards
│   ├── CollectionCardEntry.cs        ← CardPrefab2
│   ├── CardDetailPanel.cs            ← Panel_CardDetail
│   ├── DeckEditorManager.cs          ← Panel_DeckEditor
│   ├── DeckCardSlot.cs               ← Deck_Slot_0..7 (slotIndex requis)
│   ├── DeckEditorCardEntry.cs        ← CardPrefabDeckEditor
│   ├── AudioSettingsPanel.cs         ← Panel_Audio
│   ├── GraphicsSettingsPanel.cs      ← Panel_Graphics
│   ├── SettingsInitializer.cs        ← GameObject persistant MainMenu
│   ├── LanguageSettingsPanel.cs      ← TMP_Dropdown FR/EN dans Panel_Settings
│   └── PanelUpdateAvailable.cs       ← Panel_UpdateAvailable (Canvas/Panel_MainMenu), bloquant
└── Utils/Scripts/
    ├── PlayerCollection.cs            ← DontDestroyOnLoad, ALPHA_ALL_OWNED=true
    ├── DeckSaveSystem.cs             ← DontDestroyOnLoad, PlayerPrefs+JsonUtility
    ├── LocalizationManager.cs        ← DontDestroyOnLoad, charge fr.json/en.json depuis Resources/Localization/
    ├── LocalizedText.cs              ← [RequireComponent(TMP_Text)], clé dans inspecteur, s'abonne à OnLanguageChanged
    └── UpdateChecker.cs              ← sur SettingsInitializer (persistant), fetch manifest.json au démarrage
```

---

## 🧱 Système de Stacks

- 8 éléments : `Feu`, `Eau`, `Terre`, `Air`, `Lumiere`, `Tenebres`, `Astral`, `Corrosif`
- Max **10 stacks** par élément
- Bonus **mineurs** → toutes les cartes alliées
- Bonus **majeurs** (3/5 stacks) → cartes du même élément (sauf Terre et Lumière majeurs = tous alliés)

### Bonus mineurs (toutes les cartes alliées)
| Élément | Bonus |
|---|---|
| Feu | +3% dégâts/stack |
| Eau | -2% dégâts subis/stack |
| Terre | -2% dégâts subis/stack (jusqu'à -10% à 5 stacks) |
| Air | +2% chance critique/stack, max 10% (toutes cartes alliées) |
| Lumière | +2% efficacité soins/stack |
| Ténèbres | +2% DGT/stack par carte adverse en vie (toutes cartes alliées) |
| Corrosif | -1 armure/stack pour TOUTES les cartes adverses |

### Bonus majeurs
| Élément | 3 stacks | 5 stacks |
|---|---|---|
| Feu | Splash adjacents (cartes Feu) | +10% DGT critiques et +5% chance critique (cartes Feu) |
| Eau | Ennemis -5% dégâts (cartes Eau) | Ennemis -10% dégâts (cartes Eau) |
| Terre | +3 armure infinie TOUS alliés | +5 armure infinie TOUS alliés |
| Air | +5% DGT critique TOUS alliés | +10% DGT critique + 5% crit TOUS alliés |
| Lumière | Régén. 3% PV max/tour TOUS alliés | Régén. 7% PV max/tour TOUS alliés |
| Ténèbres | +5% Vol de Vie (cartes Ténèbres) | +10% Vol de Vie (cartes Ténèbres) |
| Corrosif | -5% PV Max — Toutes les cartes adverses | -10% PV Max — Toutes les cartes adverses |

---

## 🌌 Système Astral

- Copie l'élément de la carte immédiatement à sa **gauche** (slotIndex - 1)
- Donne +1 stack de cet élément
- Flèche ← à gauche de la carte Astral (identique P1 et P2)
- Si carte gauche détruite → stack retiré immédiatement
- `RefreshPermanentStacks` en **deux étapes** : cartes normales d'abord, Astral ensuite

---

## 🛡️ Système d'Armure

- Armure = **pool de PV supplémentaire** (pas un % de réduction)
- Dégâts appliqués : armure d'abord → PV si armure épuisée
- `armorPoints` dans `CardData` = valeur de base permanente (stat de carte)
- GiveArmor (buffs/passifs en combat) : cumulatifs par source, **plafonné à 20 au total** (`MAX_GIVE_ARMOR`)
- Armure totale = `armorPoints` + `min(sommeGiveArmor, 20)` − réduction Corrosif adversaire
- Perce-Armure → ignore l'armure entièrement

---

## 🌟 Système de Passifs

### Passif stacksPerTrigger
```csharp
// CardPassive
public bool stacksPerTrigger = false;  // activer la mécanique cumulatif
public int  maxTriggerStacks = 4;      // plafond de déclenchements

// CardInstance
public int passiveStackCount = 0;      // compteur runtime
```
- S'utilise avec `OnAllyDestroyed` : chaque allié de l'élément `triggerElement` détruit incrémente le passif
- L'effet appliqué = `effect.value × passiveStackCount` (remplace le précédent à chaque déclenchement)
- Si la carte source est détruite → tous ses effets (par `sourceName`) sont retirés des survivants



### Triggers valides
```
OnCardDestroyed       — quand une carte est détruite
WhenThisCardDestroysCard — quand CETTE carte détruit une carte adverse
OnStackThreshold3     — quand le joueur atteint 3 stacks d'un élément
OnStackThreshold5     — quand le joueur atteint 5 stacks d'un élément
OnAllyDestroyed       — quand un allié est détruit
OnTurnStart           — début de tour
CardIsBurning         — dynamique, PAS un événement (voir section dédiée ci-dessous)
ForEachAllyAlive      — dynamique, PAS un événement (voir section dédiée ci-dessous)
```

### ForEachAllyAlive — trigger dynamique (2026-07-10)
Même architecture que `CardIsBurning` (voir ci-dessous) — bypass complet de `PassiveManager`, recalculé à chaque attaque dans `DamageCalculator.Calculate` :
```csharp
// Bonus = effect.value (AttackBoost/AttackBoostFlat du passif) × nb d'alliés vivants
// de la carte (même ownerPlayerID), EN EXCLUANT la carte elle-même.
```
- `CountAliveAllies(card)` : `GetAliveCards(card.ownerPlayerID)` moins la carte elle-même — jamais l'adversaire
- Diminue automatiquement à mesure que des alliés meurent, jusqu'à 0 (pas de `ActiveEffect` stocké à nettoyer, pas de logique de retrait — le recalcul live gère ça tout seul)
- `stacksPerTrigger`/`maxTriggerStacks`/`triggerElement` ignorés pour ce trigger, comme pour `CardIsBurning` — le plafond naturel est déjà le nombre de cartes sur le plateau (4 alliés max hors soi-même)
- UI câblée dès la création (leçon tirée du bug `CardIsBurning`) : `preview_ally_alive_passive_fx` dans `CombatUIManager` (preview) + tag `(+N Passif Alliés)` dans `SkillExecutor.ExecuteSingleEnemy` (log de combat)
- Miroir serveur : `AstraleumCore/DamageCalculator.cs` (`GetAllyAliveFlatBonus` + `CountAliveAllies(card, board)`)
- Ajouté **en dernier** dans `PassiveTrigger` (client + serveur) — n'affecte aucune carte existante

### CardIsBurning — trigger dynamique (2026-07-10)
Contrairement aux autres triggers, **ne passe jamais par `PassiveManager.TriggerPassive`/`ResolvePassive`** (pas d'événement discret à écouter) et **ne crée aucun `ActiveEffect`/`ConditionalPassiveEffect` stocké**. Entièrement recalculé à chaque attaque, directement dans `DamageCalculator.Calculate` (+ `GetAttackerFlatBonus` pour la preview/tooltip, + `GetPreview.isAmplified`) :
```csharp
// Bonus = effect.value (AttackBoost/AttackBoostFlat du passif) × nb de cartes
// (alliées + ennemies) actuellement affectées par Burn, sur tout le plateau.
```
- Compte les cartes brûlantes des **deux joueurs confondus**, recalculé à chaque appel (pas de cache)
- Réutilise `EffectType.AttackBoost`/`AttackBoostFlat` sur les `effects` du passif — **aucun nouveau champ sur `CardPassive`/`CardEffect`**, `triggerElement`/`stacksPerTrigger` ignorés pour ce trigger
- Miroir serveur : `AstraleumCore/DamageCalculator.cs` (`GetBurningPassiveFlatBonus` + `CountBurningCardsOnField(board)`)
- Ajouté **en dernier** dans `PassiveTrigger` (client + serveur) — n'affecte aucune carte existante, aucune donnée de passif à revérifier
- **Bug initial (2026-07-10, corrigé le jour même)** : le bonus était bien calculé (confirmé par log de diagnostic en Play Mode : `burningBonus=2` correctement appliqué), mais **aucune UI ne l'affichait** — chiffre final noyé dans le total, aucun moyen pour le joueur de vérifier visuellement. Corrigé : `GetBurningPassiveFlatBonus`/`CountBurningCardsOnField` passés en `public`, ligne `preview_burning_passive_fx` ajoutée dans `CombatUIManager` (barre de preview, texte orange, avant l'attaque) + tag `(+N Passif Brûlure)` ajouté au log de combat dans `ExecuteSingleEnemy` (`SkillExecutor.cs`) — uniquement le chemin SingleEnemy, pas AllEnemies/AdjacentEnemies (scope limité au cas rapporté)

### Règles de désactivation
- Seuil 5 perdu → effets `OnStackThreshold5` retirés des cartes ennemies
- Seuil 3 perdu → effets `OnStackThreshold3` retirés des cartes ennemies
- `Duration = -1` : effet permanent TANT QUE le seuil est actif
- Géré via `ConditionalPassiveEffect` dans `CardInstance.conditionalPassiveEffects`

### TriggerElement
```csharp
public enum TriggerElement { Feu, Eau, Terre, Air, Lumiere, Tenebres, Astral, Any }
// Any = tous les éléments sauf Astral
```

### ConditionalPassiveEffect
```csharp
public class ConditionalPassiveEffect {
    public EffectType     type;
    public float          value;
    public PassiveTrigger trigger;
    public int            requiredThreshold; // 3 ou 5
    public Element        triggerElement;
    public EffectTarget   effectTarget;
    public int            ownerPlayerID;     // joueur propriétaire du passif
}
```

---

## 🌿 Système de Branche Conditionnelle

Déclaré dans `CardSkill.branches` (List<ConditionalBranch>) — fichier `Assets/Game/Cards/Scripts/SkillBranch.cs`.

### Conditions (ConditionType)
| Type | Champs supplémentaires |
|---|---|
| `TargetHPPercent` / `AttackerHPPercent` | `compareOp` (≤ ≥ =) + `threshold` (0–1) |
| `TargetHasEffect` / `AttackerHasEffect` | `compareOp` + `effectType` |
| `TargetIsBurning` / `TargetIsPoisoned` | aucun |
| `AttackerIsBurning` / `AttackerIsPoisoned` | aucun |
| `AlwaysTrue` | aucun |
| `TargetHasNoArmor` / `TargetHasArmor` | aucun |

### Effets de branche (BranchEffectType)
| Effet | Moment | Stocké |
|---|---|---|
| `DamageAmplify` | **PRE-DAMAGE** via `EvalBranchAmplify` + `extraAmplify` | Non — one-shot |
| `AttackBoost` ciblant **Attacker** | **PRE-DAMAGE** via `EvalBranchAttackBoost` + `branchAttackBoost` (même pattern que `DamageAmplify`) | Non — one-shot |
| `AttackBoost` ciblant **Target** / `AttackBoostFlat` / `AttackReduction` | Post-damage | Oui — ActiveEffect |
| `DamageReduction` / `Saignement` / `Burn` / `Poison` / `Stun` | Post-damage | Oui — ActiveEffect |
| `InstantHeal` | Post-damage | Non — `Heal()` direct, respecte HealBlock |
| `HealOverTime` | Post-damage | Oui — ActiveEffect |
| `InstantDamage` | Post-damage | Non — `TakeDamage()` direct, Flat ou % PV max |

### Règle DamageAmplify pré-damage
```csharp
// DamageCalculator.Calculate a un paramètre optionnel :
public static int Calculate(CardInstance attacker, CardSkill skill, CardInstance target, float extraAmplify = 0f)
// Dans SkillExecutor, AVANT Calculate :
float branchAmplify = EvalBranchAmplify(attacker, skill, target);
int dmg = DamageCalculator.Calculate(attacker, skill, target, branchAmplify);
// ApplyBranches() skip les branches DamageAmplify (déjà consommées)
```

### Valeur et cible
- `BranchValueMode` : `Percent` (décimal 0–1) ou `Flat` (entier)
- `BranchTarget` : `Target` (cible de la compétence) ou `Attacker`
- `durationTurns` masqué dans l'inspecteur pour `InstantHeal`

### ⚠️ Bug corrigé — AttackBoost/AttackReduction en mode Percent (2026-07-10)
`EffectType.AttackBoost`/`AttackReduction` sont **flat en jeu** (migration documentée plus haut : "AttackBoost / AttackReduction — migration flat") — `DamageCalculator.Calculate` fait `dmg += eff.value` sans jamais traiter `eff.value` comme un %. Or `EvalBranchAttackBoost` et `ApplyBranches` stockaient directement `branch.valuePercent` (ex. `0.25`) tel quel quand `valueMode = Percent`, ce qui donnait **+0.25 DGT réel au lieu de +25%** (arrondi à ~0, effet invisible). Corrigé : en mode Percent, `AttackBoost`/`AttackReduction` convertissent maintenant la valeur en `skill.damage × valuePercent` avant application/stockage. Les autres effets de branche (Saignement, DamageReduction, MaxHPReduction, etc.) n'étaient PAS affectés — leur `EffectType` consomme déjà `eff.value` comme un vrai %.
- **11 cartes affectées** (branche AttackBoost + mode Percent, jusque-là quasi sans effet, maintenant fonctionnelle) : `Card_007_Istan` (15%, Target), `Card_008_DragonDeFeu` (25%, Attacker), `Card_012_DragonDesTenebres` (5%, Attacker), `Card_015_Ilidius` (15%, Target), `Card_022_Djormund` (15%, Attacker), `Card_025_GobelinPyromane` (**50%**, Attacker), `Card_026_Eldrich` (20%, Attacker), `Card_035_Orgueil` (**50%**, Attacker), `Card_045_Arpentis le Virulent` (10%, Attacker), `Card_046_Kael&Ambroise` (25%, Attacker), `Card_Starter_Water` (25%, Attacker)
- Ces cartes vont désormais infliger sensiblement plus de dégâts quand leur condition est vraie — à re-tester/rééquilibrer, notamment Card_025 et Card_035 (50%)
- Miroir appliqué dans `AstraleumCore/SkillExecutor.cs` (client + serveur synchronisés)

### Note Saignement
Les `CardEffect` de type `Saignement` sur les SO doivent utiliser des valeurs décimales (ex. `0.05` = 5% PV max/tour). Les anciennes valeurs absolues (ex. `50`) doivent être converties.

---

## ⚙️ Enums importants

```csharp
Element:       Feu, Eau, Terre, Air, Lumiere, Tenebres, Astral, Corrosif
TriggerElement: Feu, Eau, Terre, Air, Lumiere, Tenebres, Astral, Corrosif, Any
EffectType:    Saignement(0), DamageAmplify(1), DamageReduction(2), ImmediateHeal(3),
               HealOverTime(4), HealBlock(5), GiveArmor(6), GiveArmorAdjacent(7),
               ArmorIgnore(8), AddStack(9), RemoveStack(10), CooldownReduction(11),
               CooldownIncrease(12), AttackBoost(13), AttackReduction(14), BonusAction(15),
               Stun(16), Poison(17), Burn(18), LifeSteal(19), Invisible(20),
               AttackBoostFlat(21), CritChanceBoost(22), CritDamageBoost(23),
               MaxHPReduction(24), ReduceArmor(25), AttackReductionFlat(26), Cancel(27)  ← TOUJOURS EN DERNIER
BranchEffectType: AttackBoost(0), AttackReduction(1), DamageAmplify(2), DamageReduction(3),
                  Saignement(4), Burn(5), Poison(6), Stun(7), InstantHeal(8),
                  HealOverTime(9), InstantDamage(10), AttackBoostFlat(11),
                  AddArmor(12), MaxHPReduction(13), ReduceArmor(14),
                  CritChanceBoost(15), CritDamageBoost(16), AttackReductionFlat(17), Cancel(18)  ← TOUJOURS EN DERNIER
EffectTarget:  Target, Self, AllAllies, AllEnemies, RandomAllies, RandomEnnemies,
               AdjacentEnemies
PassiveTrigger: OnTurnStart, OnCardDestroyed, OnAllyDestroyed,
                WhenThisCardDestroysCard, OnStackThreshold3, OnStackThreshold5,
                CardIsBurning  ← TOUJOURS EN DERNIER (sérialisé comme entier dans CardPassive.trigger)
CardRarity:    Commun, Rare, Epique, Legendaire, Supreme
SkillType:     Attack, Heal, Buff, Debuff, Mixed
```

> ⚠️ **CRITIQUE — Enums sérialisés** : `EffectType` et `BranchEffectType` sont stockés comme entiers dans les `.asset` Unity. Ne jamais insérer de valeur au milieu. Tout nouvel `EffectType` ou `BranchEffectType` doit être **ajouté à la fin** de l'enum pour ne pas décaler les valeurs des cartes existantes.

---

## 🔑 Règles critiques

### Duration Turns
| Valeur | Comportement |
|---|---|
| `1`, `2`, `3`... | Dure N tours |
| `-1` | Permanent (infini) |
| `0` | Ne pas utiliser |

### Soins (ImmediateHeal / HealOverTime / Régén. Lumière) — basés sur les PV MAX
```csharp
// TOUJOURS utiliser EffectiveMaxHP (client) / GetEffectiveMaxHP(stacks) (serveur) pour tout soin
int heal = Mathf.RoundToInt(EffectiveMaxHP * effect.value);
// JAMAIS currentHP * value pour un soin (revert 2026-07-10 : % PV restants trop faible en clutch)
```
Le soin est une valeur fixe basée sur les PV max effectifs (tient compte de MaxHPReduction/Corrosif), peu importe l'état actuel de la cible. Ne s'applique qu'aux soins — Saignement/Burn/Poison/MaxHPReduction restent en % de `data.maxHP` (voir plus bas).

### Heal() — paramètre showPopup
```csharp
card.Heal(amount, true);   // soin direct via compétence → popup visible
card.Heal(amount, false);  // soin via stack/passif → popup géré ailleurs
```

### ProcessActiveEffects — ordre séquencé
1. Effets Saignement/Burn/Poison → dégâts appliqués, `dotTotal` accumulé
2. Effets HealOverTime → soins appliqués, `hotTotal` accumulé
3. `ShowEffectPopupsSequenced(dotTotal, hotTotal)` :
   - Popup rouge DoT affiché
   - 2 secondes d'attente
   - Popup rouge masqué → popup vert HoT affiché

### Singleton UI Panel — pattern obligatoire
Tout panel avec un `static Instance` qui démarre inactif doit suivre ce pattern pour que `Instance` soit assigné dès le chargement :
```csharp
private void Awake()
{
    Instance = this;
    // setup optionnel...
    gameObject.SetActive(false); // ← dans Awake, PAS dans Start()
}
```
**Le GO doit être actif dans la scène** (sauvegardé actif) pour que `Awake()` s'exécute au chargement.
Mettre `SetActive(false)` dans `Start()` provoque un bug : au premier clic, `Awake` + `OnEnable` + `Start` s'enchaînent → `Start` re-cache le panel aussitôt.
Panels concernés : `CardDetailPanel`, `DeckSelectPanel`, `TargetingArrow`, `DeckEditorSkillPreview`.

### ProcessActiveEffects — par joueur
- `OnTurnStart` → appelle `ProcessActiveEffects` sur les cartes du **joueur actif uniquement**
- Les cartes adverses traitent leurs effets quand **c'est leur tour**

### LifeSteal — Vol de Vie
```
value = % des DGT volés (ex. 0.03 = 3%)
durationTurns = -1  → immédiat par attaque (non stocké comme buff)
durationTurns > 0   → buff persistant stocké dans activeEffects
```
- Déclenché après chaque `TakeDamage` : SingleEnemy, AllEnemies (par ennemi), AdjacentEnemies (cible principale + adjacents)
- Cumule les effets immédiats de la compétence ET les buffs persistants actifs sur l'attaquant
- `SkillExecutor.ApplyLifeSteal(attacker, skill, dmgDealt)` est le point d'entrée unique

### Invisible — Non-ciblable
```
Passif OnTurnStart : effect type=Invisible, value=1, durationTurns=-1, effectTarget=Self
```
- Bloque le ciblage `SingleEnemy` (clic + preview)
- N'affecte PAS `AllEnemies`, `AdjacentEnemies` (splash/AoE passent)
- `CardInstance.UseSkill()` retire automatiquement l'effet
- Le passif re-applique Invisible au début de chaque tour du joueur
- `CardInstance.IsInvisible` → propriété booléenne de vérification rapide

### Saignement / Burn / Poison / HealBlock / HOT / DamageAmplify — Icônes de statut
- **Burn** : affectée armure + DamageReduction. Icône `BurnIcon` via `CardVisualUpdater.burnIcon` + `IconLibrary.iconBurn`
- **Poison** : ignore armure. Icône `Poison` via `CardVisualUpdater.poisonIcon` + `IconLibrary.iconPoison`
- **Saignement** : dégâts % PV max/tour, empile par source. Icône `Bleed` via `CardVisualUpdater.saignementIcon` + `IconLibrary.iconSaignement`
- **HealBlock** : icône `HealBlock` via `CardVisualUpdater.healBlockIcon` + `IconLibrary.iconHealBlock`
- **HOT** : icône `HOT` via `CardVisualUpdater.hotIcon` + `IconLibrary.iconHOT`
- **DamageAmplify** (2026-07-10) : uniquement pour un `EffectType.DamageAmplify` **stocké** (skill à `durationTurns > 0`) — la version branche conditionnelle (`BranchEffectType.DamageAmplify`) est consommée pré-damage en one-shot et n'est jamais stockée, donc jamais affichée par cette icône (comportement voulu). Icône `DamageAmplify` via `CardVisualUpdater.damageAmplifyIcon` + `IconLibrary.iconDamageAmplify`
- Toutes ces icônes sont auto-trouvées par nom exact via `FindChildImage`/`ForceDisableChild` (`FindReferences`) — le nom du GameObject dans le prefab doit correspondre exactement (`Poison`, `Bleed`, `HealBlock`, `HOT`, `DamageAmplify`)
- Multi-source Saignement/Burn : même source → durée/valeur rafraîchie ; source différente → nouvelle instance empilée
- Tooltip Burn : clé `buff_burn_line` (FR/EN)
- `FindReferences` : `SetActive(false)` appelé **inconditionnellement** au chargement pour toutes ces icônes (hors du guard `if (== null)`) — sinon les refs pré-assignées en inspector ne sont jamais cachées au démarrage
- `Update*Icon` : `SetActive(true)` uniquement si `sprite != null` — évite le carré blanc quand le sprite n'est pas assigné dans IconLibrary

### Règle d'empilement des effets actifs (`ApplyEffect`)
| Effet | Comportement |
|---|---|
| Burn / Saignement | Même source → rafraîchit durée/valeur ; source différente → nouvelle instance |
| Poison | Toujours mergé : valeur max, durée max (1 seule instance) |
| DamageReduction / AttackReduction | Cumulatif additif, plafonné à 50% (1 seule instance) |
| GiveArmor | Même source+skill → rafraîchit ; source différente → nouvelle instance (plafond total 20 dans TotalArmor) |
| **Tous les autres** | **Toujours empilés comme instances indépendantes** — une carte peut avoir plusieurs AttackBoost, HealOverTime, HealBlock, Stun, etc. simultanément |

### TriggerHitShake — Vibration sur coup direct
- `CardVisualUpdater.TriggerHitShake()` → coroutine 0.25s, magnitude 6px, decroissante
- Appelé depuis `CombatPopupHandler.ShowDamagePopup()` — donc sur TOUT coup direct (Single/AoE/Adjacent)
- N'affecte PAS les ticks DoT/Burn/Poison (ceux-ci passent par `ShowEffectPopupsSequenced`, pas `ShowDamagePopup`)

### BonusAction — Action supplémentaire
```
value = nombre d'actions bonus (entier, ex. 1)
durationTurns ignoré — effet immédiat
```
- Incrémente `CardInstance.bonusActionsRemaining`
- La carte peut rejouer même si `hasActedThisTurn = true`, tant que `bonusActionsRemaining > 0`
- Consomme quand même une action globale (`TurnManager.actionsRemaining`)
- `bonusActionsRemaining` remis à 0 à la fin du tour (`TurnManager.EndTurn`)

### Stun — Étourdissement
- Décrémenté à la **FIN du tour** du joueur affecté (dans `TurnManager.EndTurn`), PAS dans `ProcessActiveEffects`
- `CardClickHandler` bloque l'ouverture du SkillPanel si la carte est étourdie
- `CardVisualUpdater` affiche l'`ExhaustedOverlay` sur les cartes éttourdies (même visuel que "a déjà joué")
- `durationTurns = 1` → bloque le joueur pendant exactement 1 tour complet

### CooldownIncrease — +1 tour de recharge sur les deux compétences (2026-07-10)
- **Modifie directement `skill1Cooldown` ET `skill2Cooldown`** (+1 sur les deux, même si prêtes) — visible immédiatement sur les icônes de compétence, contrairement à l'ancien gel invisible
- En plus de ce +1, stocke un `ActiveEffect{type=CooldownIncrease, remainingTurns=1}` sur la cible pour **bloquer le décompte normal pendant exactement 1 tour** (sinon le +1 serait immédiatement annulé par le décompte du tour suivant)
- `CardInstance.OnTurnStart()` : si cet effet est présent, **skip le décompte** des cooldowns ce tour (mais `ProcessActiveEffects` tourne normalement)
- `TurnManager.EndTurnLocal()` : effet retiré à la **FIN du tour** du joueur affecté — exactement le même schéma que le Stun (voir ci-dessus) ; le décompte normal reprend ensuite son cours
- Effet net = bloque les deux compétences jusqu'à la fin du **prochain tour complet** de la cible, quel que soit leur cooldown au moment du cast (corrige l'ancien flaw où un cooldown déjà à 0 n'était pas affecté par le gel)
- `effect.value` ignoré — la durée du blocage est toujours fixe (1 tour de la cible), seul le +1 sur les deux cooldowns est fixe
- Textes UI/log renommés de "Recharge gelée" → "Recharge +1 tour" (`buff_cdinc_line`, `preview_cd_inc_fx`, `CombatLogManager.DescribeEffect`)

### CooldownReduction — jamais sur la carte qui lance la compétence (2026-07-10)
- `SkillExecutor.ApplyEffectToCard` : guard `if (target == source) break;` — un CooldownReduction ciblé sur soi-même (SingleAlly) n'a aucun effet
- `SkillExecutor.ApplyEffect` (dispatch AllAllies / RandomAllies) : la source est exclue de la liste des alliés avant application/tirage, uniquement pour `EffectType.CooldownReduction` (les autres effets AllAllies/RandomAllies continuent d'inclure le lanceur normalement)
- Miroir appliqué dans `AstraleumCore/SkillExecutor.cs` (client + serveur synchronisés)

### Passifs — Règles d'implémentation
- **Toutes raretés** peuvent avoir un passif (le check Legendaire/Supreme a été supprimé) — la `passiveDescription` non-vide est la seule garde
- `BoardManager.DestroyCard` appelle déjà `PassiveManager.OnCardDestroyed` en interne → ne jamais rappeler `OnCardDestroyed` directement après `DestroyCard` (double déclenchement)
- **OnAllyDestroyed** : filtre `triggerElement` appliqué ; `resolvedSource = card` (la carte détruite n'est plus alive, ne jamais la passer comme cible d'effet)
- Compétences `Self` : `HighlightValidTargets` surligne uniquement la carte sélectionnée via `HighlightSelfTarget()`

### VFX — Positionnement sur carte (CardVFXHandler)
- `AnchorPos` utilise `rt.TransformPoint(rt.rect.center)` si `vfxAnchor` non assigné → centre visuel exact indépendant du pivot
- Assigner `vfxAnchor` dans le prefab pour un ancrage personnalisé

### ActionBar — DOTs et Bouton Fin de Tour
- DOT1 / DOT2 : conservent leur couleur d'origine, sprite swap vers `spriteActionUsed` (big_roundframe) quand l'action est consommée
- `spriteActionUsed` assigné dans l'Inspector sur `CombatUIManager`
- Bouton Fin de Tour : `btnFinTourNormal` au repos, `btnFinTourHighlight` + outline pulsante quand toutes les actions sont épuisées

### Réseau LAN (Mirror) — Règles critiques

**Identité réseau**
```csharp
NetworkBridge.LocalPlayerID = NetworkServer.active ? 0 : 1;  // Awake()
NetworkBridge.IsActive      = LocalPlayerID >= 0;
```

**Perspective — spawn basé sur localID (JAMAIS un flip visuel ni un swap de slots)**
`BoardSpawner.SpawnAllCardsNetwork` place toujours les cartes **du joueur local** dans `player1Slots` (bas) et les cartes **adverses** dans `player2Slots` (haut), quel que soit le `LocalPlayerID`. Les cartes conservent leur `ownerPlayerID` réel (0=J1, 1=J2) — `GetCardAtSlot(playerID, slotIndex)` reste cohérent sur tous les clients car il cherche par `(ownerPlayerID, slotIndex)` dans `allCards`, indépendamment de la position physique. Après spawn, `StackDisplayManager.ApplyNetworkPerspective(LocalPlayerID)` est appelé pour remapper l'affichage des stacks.
- `FlipBoardVisualForP2()` a été supprimé — ne jamais le recréer.
- `SwapSlotsForP2Perspective()` a été supprimé de `BoardManager` — ne jamais le recréer.
- Swap de slots ou flip visuel = `GetCardAtSlot` incohérent = HP faux + `hasActedThisTurn` sur mauvaise carte.

**Calcul isEnemy dans tous les handlers**
```csharp
// Pattern à utiliser partout (CardClickHandler, CardHoverHandler, CombatUIManager)
int perspectivePlayer = NetworkBridge.IsActive
    ? NetworkBridge.LocalPlayerID
    : (TurnManager.Instance?.currentPlayerID ?? 0);
bool isEnemy = card.ownerPlayerID != perspectivePlayer;
```

**Enemy/Ally IDs dans CombatUIManager**
```csharp
int enemyID = NetworkBridge.IsActive ? 1 - NetworkBridge.LocalPlayerID : (currentPlayerID == 0 ? 1 : 0);
int allyID  = NetworkBridge.IsActive ? NetworkBridge.LocalPlayerID      : currentPlayerID;
```

**Arrow remote highlight**
- Sélection skill → `NetworkBridge.OnArrowShowRequested(ownerPlayerID, slotIndex)`
- `OnClientArrowUpdate` : filtre `attackerPlayerID == LocalPlayerID`, puis `GetCardAtSlot` + `ActivateHighlight(Attack)`

**autoCreatePlayer = false** dans `AstraleumNetworkManager.Awake()` — supprime le warning "PlayerPrefab is empty".

---

## 🖥️ UI — Structure scène Combat

```
Canvas
├── DamagePreviewBar (largeur auto ContentSizeFitter +100px padding droit, hauteur 105, Top Center, Pos Y -65)
├── StackDisplay_P1 (bas gauche) — 7 icônes élémentaires + compteur
├── StackDisplay_P2 (haut gauche)
├── StackTooltip (désactivé par défaut)
│   ├── TooltipHeader (TMP_Text)
│   └── TooltipLines (IconLineManager)
├── PassiveTooltip (désactivé par défaut)
├── Board (plateau 10 slots)
├── HUD_Top / HUD_Bot
├── ActionBar (timer + dots + Btn_FinTour + Btn_Abandon)
├── CombatLog
├── TurnAnnouncement (CanvasGroup alpha=0 au repos, TurnAnnouncementManager)
└── CardSkillPanel
```

---

## 🃏 CardPrefab — Structure

```
CardPrefab
├── Artwork (Image)
│   ├── PassiveIcon (coin inférieur droit)
│   └── AstralArrow (flèche directionnelle)
├── SkillZone
├── HealPopup (TMP_Text — vert)
├── DamagePopup (TMP_Text — rouge)
├── BurnIcon (Image — icône brûlure, désactivé par défaut)
├── PoisonIcon (Image — icône poison, désactivé par défaut) ← à créer dans le prefab
├── SaignementIcon (Image — icône saignement, désactivé par défaut) ← à créer dans le prefab
├── InvisibleOverlay (Image — overlay bleu)
└── DestroyedOverlay
```

---

## ✅ Fonctionnalités validées

- [x] Système stacks complet (permanents/temporaires/Astral)
- [x] Armure = réduction plate par coup (pas pool HP)
- [x] Terre mineur : -2% DGT subis/stack (passif DamageCalculator), max -10% à 5 stacks
- [x] Terre majeur : +3 armure infinie (3 stacks) / +5 armure infinie (5 stacks), supprimée si seuil perdu
- [x] Passifs conditionnels (OnStackThreshold3/5) avec ConditionalPassiveEffect
- [x] Désactivation effets passifs sur perte de seuil
- [x] DOT et HoT séquencés (DOT d'abord, HoT 2s après)
- [x] DOT/HoT uniquement sur le joueur actif (au début de son tour)
- [x] HoT calculé sur data.maxHP (pas currentHP)
- [x] Flèche directionnelle Astral (← gauche, identique P1 et P2)
- [x] Stack Astral : RefreshPermanentStacks en deux étapes
- [x] ShowHealPreview : valeur fixe en PV (pas %)
- [x] ShowBuffPreview : affiche soin + buffs en valeur fixe
- [x] Fin de tour manuelle + BtnFinTourGlow (Outline pulsant)
- [x] Son début de tour P1/P2 (TurnAudioManager)
- [x] Preview AoE Adjacent (ShowAdjacentDamagePreview)
- [x] Compétences d'attaque bloquées sur cartes alliées
- [x] Icône passif dans coin inférieur droit artwork
- [x] Tooltip passif au clic inspection carte adverse
- [x] Actions limitées à 2 par tour
- [x] ConditionalPassiveEffect AllEnemies : DOT appliqué aux deux joueurs
- [x] ActiveEffect.sourceName : traçabilité source de chaque effet
- [x] DamageReduction : correction double application + DoT/Poison non réduits
- [x] DamageReduction : cumulatif additif plafonné à 50%
- [x] BuffTooltipManager : tooltip fixe bord droit au survol (effets actifs + source colorée + HoT en PV)
- [x] DamagePreviewBar complet : CPE AttackBoost, Eau majeur, DamageReduction séparé, effets secondaires (DoT/Drain/Poison/LifeSteal)
- [x] SkillExecutor : double dégâts corrigé, OnCardDestroyed centralisé dans HandleCardDeath
- [x] DamageCalculator.Calculate : source unique pour tous les types d'attaque (Single/AoE/Adjacent)
- [x] Passif stacksPerTrigger : accumulation par allié d'élément X détruit (max 4), annulé si source détruite
- [x] PV carte : texte toujours blanc (suppression du changement orange/rouge)
- [x] Panel_MyCards : grille collection, filtres rareté/élément, CardDetailPanel (artwork + compétences + passif + lore adaptatif)
- [x] Panel_DeckEditor : 8 slots, états Empty/Editing/Saved, couleur élément dominant persistée, numéro d'ordre sélection (1-5)
- [x] DeckCardSlot : persistance autonome PlayerPrefs (Slot_[i]_State/Name/Element), slotIndex requis dans inspecteur
- [x] DeckSaveSystem : SavedDeck avec slotIndex + dominantElementIndex, GetDeckBySlot(int)
- [x] DeckManager.TryAddCard : fallback Resources.LoadAll si CardDatabase absent
- [x] Panel_Audio : 4 sliders + toggle musique, AudioMixer dB, PlayerPrefs.Save() immédiat
- [x] Panel_Graphics : qualité, résolution, plein écran, VSync, particules (événement statique), PlayerPrefs.Save() immédiat
- [x] SettingsInitializer : applique audio + graphiques au lancement (Start, DontDestroyOnLoad)
- [x] LocalizationManager : FR/EN, JSON flat compatible Weblate, EnsureInitialized() lazy init, GetCard(n, field, fallback) avec fallback SO
- [x] LocalizedText : composant TMP_Text auto-rafraîchi sur OnLanguageChanged (83 MainMenu + 25 Combat)
- [x] Traduction UI complète : MainMenu (tous panels) + Combat (ActionBar, CombatLog, DamagePreviewBar, EndGame, GiveUp)
- [x] CardDetailPanel : skills + passif + lore + quote via GetCard() avec fallback ScriptableObject
- [x] CardSkillPanel : panneau vertical positionné à droite de la carte sélectionnée (ScreenSpaceCamera fix)
- [x] CardSkillPanel : Frame_big rendue en dernier enfant (dessus des boutons), Image parent désactivée, DarkenOverlay sur chaque skill (cooldown = assombrissement, pas transparence)
- [x] CardSkillPanel ennemi (readOnly) : couleurs normales, boutons non-cliquables, pas d'overlay
- [x] CardClickHandler : guard anti-repositionnement (même carte = panel ne se réouvre pas), `currentReadOnlyCard` séparé pour le mode readOnly
- [x] PassiveTooltip : Show() appelé à l'ouverture du SkillPanel (allié et ennemi), Hide() à la fermeture/annulation
- [x] TargetingArrow : flèche depuis centre carte → curseur, couleur par type (rouge Attack/Debuff, vert Heal/Buff), ArrowHead triangle UIVertex
- [x] Cartes adverses : SkillPanel lecture seule (compétences non cliquables)
- [x] Guard hasActedThisTurn dans CardClickHandler (empêche double action par tour)
- [x] CollectionCardEntry : clic ouvre CardDetailPanel (Panel_CardDetail doit démarrer actif en scène)
- [x] DeckSelectPanel : Btn_Unranked fonctionne en un clic (SetActive(false) déplacé de Start() vers Awake(), Panel_DeckSelect actif en scène)
- [x] Panel_HowToPlay : panel tutoriel MainMenu, 7 sections localisées FR/EN, ScrollView avec ContentSizeFitter par élément, inactif par défaut, Btn_Tuto → OpenHowToPlay() / Btn_Close → GoBack()
- [x] CombatLog : messages courts, numéro de tour (T1/T2...), couleur par joueur (J1=vert, J2=rouge-orangé), namespace Astraleum ajouté
- [x] Saignement (ex-DotDamage) : cumul multi-sources indépendants (clé sourceName+sourceSkillName), même source recaste → durée rafraîchie
- [x] EffectTarget RandomAllies / RandomEnnemies : cible un allié/ennemi aléatoire vivant
- [x] LifeSteal : vole X% des DGT infligés (durationTurns=-1 = par attaque, >0 = buff persistant), fonctionne SingleEnemy/AllEnemies/Adjacent, preview barre dégâts
- [x] Invisible : passif OnTurnStart (Invisible, durationTurns=-1, Self) — non ciblable par SingleEnemy, passe les AoE/Adjacent, perdu à l'action, restauré au tour suivant
- [x] Stun : décrémenté en fin de tour (TurnManager.EndTurn), bloque le SkillPanel, ExhaustedOverlay visuel, durationTurns=1 = 1 tour complet bloqué
- [x] VFX centré sur la carte : CardVFXHandler.AnchorPos utilise rt.TransformPoint(rt.rect.center)
- [x] Passif OnAllyDestroyed : filtre triggerElement + resolvedSource=card + toutes raretés supportées + double appel OnCardDestroyed supprimé
- [x] Highlight Self : SkillTargetType.Self → HighlightSelfTarget() (carte caster uniquement, pas tous les alliés)
- [x] ActionBar DOTs : sprite swap big_roundframe à l'usage, couleur d'origine conservée
- [x] Bouton Fin de Tour : couleur normale au repos, prononcée (btnFinTourHighlight + outline pulsante) quand actions épuisées
- [x] EffectType.Burn : brûlure % PV max/tour, affectée armure + DamageReduction (≠ Poison qui ignore armure), une instance, icône Icon_Burn via CardVisualUpdater.burnIcon + IconLibrary.iconBurn, tooltip buff_burn_line FR/EN
- [x] EffectTarget.AdjacentEnemies : applique l'effet à la cible principale + ses voisins ennemis (réutilise BoardManager.GetAdjacentCards)
- [x] EffectType.BonusAction : accorde x actions supplémentaires à une carte (CardInstance.bonusActionsRemaining), consomme une action globale, réinitialisé à 0 en fin de tour
- [x] Réseau LAN — Mirror v96, host-autorité, AstraleumNetworkManager (DontDestroyOnLoad, autoCreatePlayer=false)
- [x] Réseau LAN — Perspective : spawn basé sur LocalPlayerID — cartes locales toujours en player1Slots (bas), adverses en player2Slots (haut), ownerPlayerID réel conservé
- [x] Réseau LAN — CardClickHandler / CardHoverHandler : perspective basée sur LocalPlayerID (pas currentPlayerID)
- [x] Réseau LAN — Highlight/AoE enemyID/allyID basés sur LocalPlayerID dans CombatUIManager
- [x] Réseau LAN — Arrow remote highlight : OnClientArrowUpdate + ActivateHighlight(Attack) sur carte attaquante adverse
- [x] Réseau LAN — Snapshot serveur → clients : HP/Armor/hasActedThisTurn/effects synchronisés via NetMsgGameState
- [x] Branche Conditionnelle (ConditionalBranch) : IF condition THEN effet, 10 conditions, 10 effets, cible Attacker/Target, valeur %/Flat, drawer éditeur contextuel
- [x] DamageAmplify branche pré-damage : `EvalBranchAmplify` + `DamageCalculator.Calculate(extraAmplify)` — one-shot, non stocké
- [x] InstantHeal / HealOverTime disponibles comme effets de branche
- [x] Conditions TargetIsBurning / TargetIsPoisoned / AttackerIsBurning / AttackerIsPoisoned
- [x] StackTooltip : largeur préservée — `linesRT.sizeDelta` non réinitialisé, seul `tooltipRT.sizeDelta.y` remis à 0
- [x] TurnTimer : texte Blanc >60s, Jaune ≤60s, Rouge ≤30s (fond retiré)
- [x] Saignement / Burn / Poison : dégâts calculés en % des PV max (`data.maxHP * effect.value`)
- [x] EffectType.DotDamage renommé en Saignement partout (Enums, CardInstance, SkillExecutor, SkillBranch, BuffTooltipManager, CombatUIManager, CardSkill)
- [x] LifeSteal popup : affiché sous la carte attaquante (offset Y -90) via ShowHealPopup avec showPopup:false
- [x] PoisonIcon + SaignementIcon : champs CardVisualUpdater + IconLibrary (sprites à assigner) — GameObjects à créer dans CardPrefab
- [x] TriggerHitShake : vibration 0.25s sur tout coup direct via CombatPopupHandler.ShowDamagePopup()
- [x] DamagePreviewBar : largeur +100 (padding droit 15→115), taille de police -2 sur les 8 champs texte
- [x] Ténèbres Poison timing : `ApplyPoisonToEnemies(oldPlayerID)` avant `OnTurnStart` dans `TurnManager.EndTurnLocal`
- [x] CooldownIncrease : bug `> 0` guards supprimé — `IncreaseCooldown` ajoute toujours `amount` aux deux cooldowns (même si à 0)
- [x] EffectType.AttackBoostFlat : bonus dégâts fixe (+N DGT absolus) ajouté après la chaîne multiplicative dans DamageCalculator ; BranchEffectType + ToEffectType + BuffTooltipManager (buff_atkflat_line) + DamagePreviewBar (preview_atkflat_bonus / preview_boost_atkflat) complets
- [x] Enum shift corrigé : AttackBoostFlat déplacé en fin de EffectType (=21) et BranchEffectType (=11) — toutes les données .asset cartes restaurées (Poison=17, Burn=18, LifeSteal=19, Invisible=20)
- [x] CardVisualUpdater icônes statut : FindReferences cache inconditionnellement burnIcon/poisonIcon/saignementIcon au démarrage ; Update*Icon n'active l'icône que si sprite != null (no white square)
- [x] TurnAnnouncementManager : pop-up centré 1-2s au début de chaque tour (fade + scale easeOut/easeIn), `VOTRE TOUR` (vert) / `TOUR ADVERSE` (rouge), network-aware (LocalPlayerID), appelé dans TurnManager.Start() et EndTurnLocal()
- [x] DeckEditorSkillPreview : clic droit sur carte dans Panel_DeckEditor → panel skills/passif (400×554px auto-height), sprite bar_ready blanc Sliced, childControlHeight=true sur tous les VLG, LayoutRebuilder.ForceRebuildLayoutImmediate() au Show()
- [x] Incantation — cible morte à la résolution : `ResolveIncantations` annule proprement (log + Remove) au lieu de crasher `SkillExecutor` sur target null, ce qui bloquait `EndTurnLocal` en permanence ; null-guards ajoutés dans ExecuteSingleEnemy/SingleAlly/AdjacentEnemies
- [x] Soins basés sur PV restants : ImmediateHeal, HealOverTime (skills + branches) et régén. Lumière utilisent `currentHP * value` au lieu de `data.maxHP * value` — CardInstance, SkillExecutor, EffectManager, CombatUIManager (previews), BuffTooltipManager, CardVisualUpdater, AIActionScorer. Les dégâts (Saignement/Burn/Poison/MaxHPReduction/InstantDamage) restent inchangés sur PV max. **Déployé sur VPS 2026-07-09.**
- [x] EffectType.CooldownIncrease repensé : gèle la recharge de la cible jusqu'à la fin de son prochain tour (au lieu d'ajouter X tours) — ActiveEffect + skip decrement dans OnTurnStart + retrait en fin de tour dans TurnManager.EndTurnLocal (même schéma que Stun). IncreaseCooldown() supprimé. 9 descriptions de cartes mises à jour (FR). Miroir appliqué dans AstraleumCore (TurnProcessor.EndTurn + SkillExecutor). **Déployé sur VPS 2026-07-09** (Core+Server rebuild + cards.json régénéré via Astraleum/Exporter cards.json, 53 cartes).
- [x] BuffTooltipManager : effets similaires fusionnés sur une seule ligne — `GetTooltipGroupKey` (AttackBoost+AttackBoostFlat et AttackReduction+AttackReductionFlat regroupés, le reste par EffectType) + `MergeEffects` (valeurs sommées, durée = la plus longue restante/∞, sources listées). Recalculé à chaque affichage depuis `card.activeEffects` en direct → se met à jour tout seul quand des instances expirent. `GetEffectTooltip` (tooltips icônes de statut) fusionne aussi.
- [x] DropdownToggle (Panel_Play, Btn_Training) : placement chaotique corrigé — `anchoredPosition`/`DOAnchorPosY` remplacés par `localPosition`/`DOLocalMoveY` (les items ont un ancrage différent du bouton toggle, anchoredPosition n'était pas comparable). `ForceCollapse()` public ajouté + câblé sur `DeckSelectPanel.trainingDropdown` : Btn_Normal referme le dropdown IA avec la même animation.
- [x] DropdownToggle : bug de clic rapide corrigé — `cg.DOKill()` ajouté partout à côté de `rt.DOKill()` (Expand/Collapse/OnEnable/OnDestroy). Un Collapse() interrompu par un Expand() rapproché laissait l'ancien tween de fade survivre et re-masquer l'item après coup (`OnComplete` non tué) — repro : 3 clics rapprochés sur Btn_Training.
- [x] DropdownToggle simplifié en fondu pur (plus de slide) : après un 2e rapport de bug de placement malgré les corrections précédentes, le glissement de position (`DOLocalMoveY` + Ease.OutBack) a été retiré entièrement. Les items ne sont plus JAMAIS déplacés — seule leur transparence (`CanvasGroup.alpha`) est animée en cascade (`staggerDelay`). Garantit par construction qu'ils restent exactement à la position éditeur, quel que soit l'état (actif/inactif, clic rapide, panel ré-activé). Testé robuste via `DOTween.CompleteAll` après clics simples et rapides répétés.
- [x] Btn_EndTurn (`ActionBar`) : raccourci clavier Espace ajouté dans `TurnManager.Update()` — appelle `EndTurn()`, identique au clic (mêmes guards réseau/tour existants, aucune logique dupliquée)
- [x] Panel_UpdateAvailable raccordé : `UpdateChecker.cs` (déjà écrit, non câblé) ajouté sur `SettingsInitializer` (persistant) — fetch `manifest.json` GitHub au démarrage, compare à `Application.version`, appelle `PanelUpdateAvailable.Instance.Show(version)` si plus récent. Panel remis actif dans la scène (obligatoire pour Awake/singleton). Bug trouvé en route : `Title`/`Desc` avaient hérité de `LocalizedText` avec des clés d'un tout autre panel (`ui_first_launch_*`, panel de bienvenue) — supprimés (ce sont maintenant `versionText`/`messageText` pilotés par script) ; `Desc2` (avertissement statique) recorrigé avec sa propre clé `ui_update_desc2`. `PanelUpdateAvailable.Show()` passe maintenant par `LocalizationManager.Get()` (FR/EN) au lieu de texte français en dur. Testé en Play Mode : `Show("9.9.9")` affiche correctement, Btn_Quit (déjà câblé sur `GameManager.QuitGame()`) ferme le jeu.

---

## 🔧 À faire

- [x] Panel_EndGame_Unranked : affiché à la victoire (cartes détruites) et à l'abandon, titre Victoire/Defaite selon perspective locale, Btn_BackToMainMenu → ReturnToMainMenu()
- [x] GiveUp — abandon : le perdant va directement au MainMenu (pas de Panel_EndGame), le gagnant reçoit Panel_EndGame Victoire. En réseau : délai 1s avant ReturnToMainMenu (loser) pour que le message arrive avant StopHost()
- [x] Btn_Quit MainMenu : wiring via MenuManager.Start() → btnQuit.onClick.AddListener(QuitGame) → GameManager.Instance?.QuitGame() → Application.Quit()
- [ ] BUG : Espace vide en bas de la bulle tooltip stack
- [ ] Passifs Suprêmes : pas de passifs spécifiques à coder (Nyolung, Djormund, Eldrich utilisent le système standard)
- [ ] Animations d'attaque et effets visuels
- [ ] Sauvegarde des stats (victoires, défaites, PR)
- [ ] Photon PUN 2 (Online PvP — Phase 2)
- [ ] Remplacement caractères Unicode → icônes PNG (CardSkillPanel/HUD/CombatLog restants)
- [ ] Panel_Accessibility — à faire
- [ ] Localisation : remplir descriptions de cartes dans fr.json/en.json (vides, fallback SO actif)
- [ ] Localisation : ajouter LocalizationManager.Get() dans CombatUIManager pour BuffLabel/ArmorLabel/HealLabel (dynamiques)
- [x] Setup Unity : panel BuffTooltip créé et fonctionnel
- [x] Créer PoisonIcon et SaignementIcon dans CardPrefab + assigner iconPoison/iconSaignement dans IconLibrary SO
- [x] Icône DamageAmplify dans CardPrefab2 + IconLibrary.iconDamageAmplify (2026-07-10)

---

## 📝 Notes de session

- Transcripts disponibles dans `/mnt/transcripts/`
- Page Notion principale : `330335fc-9493-81bf-bcb0-d11d221d4464`
- Page parent Game Design : `32b335fc-9493-8126-a11d-dc97f0911c76`
