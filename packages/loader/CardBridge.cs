using System;
using System.Collections.Generic;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Models;
using UnityEngine;

namespace PancakeModLoader
{
    public class CustomTargetConfig
    {
        public string Scope { get; set; } = "single";
        public int Count { get; set; } = 1;
        public int Row { get; set; } = -1; // -1: auto/front, -2: back, -3: all, >=0: specific group index
        public int Column { get; set; } = -1; // -1: any, >=0: specific column index
        public bool Pierce { get; set; } = false;
        public bool Random { get; set; } = false;
    }

    public class CustomEffectsConfig
    {
        public int Freeze { get; set; } = 0;
        public bool Disarm { get; set; } = false;
        public bool Knockback { get; set; } = false;
        public bool Erase { get; set; } = false;
        public int Damage { get; set; } = 0;
        public int Armor { get; set; } = 0;
        public int Heal { get; set; } = 0;
        public int Mana { get; set; } = 0;
        public int Draw { get; set; } = 0;
        public int Might { get; set; } = 0;
        public int Coins { get; set; } = 0;
        public int Xp { get; set; } = 0;
        public int Combo { get; set; } = 0;
        public bool ReverseCombo { get; set; } = false;
        public bool Minecart { get; set; } = false;
        public int MinecartDamage { get; set; } = 0;
        public string Sound { get; set; } = "";
    }

    public class CustomCardDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public string Color { get; set; } = "attack";
        public int Mana { get; set; } = 1;
        public int Damage { get; set; }
        public int Armor { get; set; }
        public int GemSlots { get; set; } = 1;
        public bool Retain { get; set; } = false;
        public bool FreeToPlay { get; set; } = false;
        public string IconPath { get; set; } = "";
        public string BodyPath { get; set; } = "";
        public string ModDirectory { get; set; } = "";
        public string Rarity { get; set; } = "Common";
        public int Weight { get; set; } = 100;
        public List<string> Pools { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public CustomTargetConfig Target { get; set; } = new();
        public CustomEffectsConfig Effects { get; set; } = new();
        public Action<PlayerModel, CardModel, IEncounterModel> OnPlayAction { get; set; }
    }

    public static class CardBridge
    {
        public static List<CustomCardDef> RegisteredDefs { get; } = new();
        public static List<CardConfig> CustomCardConfigs { get; } = new();
        private static bool _cardsCreated = false;

        public static void Init(DevConsole console)
        {
            // Initialized with console
        }

        public static void RegisterCard(CustomCardDef def)
        {
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            ModRegistry.NoteCard(def.Id, def.Rarity, def.Weight, def.Pools);
            LoaderLog.Info($"[CardBridge] Registered custom card: {def.Id} ('{def.Name}', {def.Mana} mana, {def.Color}) IconPath='{def.IconPath}' BodyPath='{def.BodyPath}' ModDir='{def.ModDirectory}'");
        }

        public static void EnsureCustomCardsCreated()
        {
            if (_cardsCreated && CustomCardConfigs.Count > 0) return;
            if (RegisteredDefs.Count == 0) return;

            try
            {
                CardConfig attackTemplate = null;
                CardConfig defenseTemplate = null;
                CardConfig skillTemplate = null;
                CardConfig wildTemplate = null;
                CardConfig crawlerTemplate = null;
                CardConfig fallbackTemplate = null;

                var objects = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<CardConfig>());
                if (objects != null && objects.Length > 0)
                {
                    for (int i = 0; i < objects.Length; i++)
                    {
                        var c = objects[i]?.TryCast<CardConfig>();
                        if (c == null || string.IsNullOrEmpty(c.name) || c.name.Contains("demo-mod") || c.name.StartsWith("Test", StringComparison.OrdinalIgnoreCase) || c.isEvolvedCard) continue;

                        if (fallbackTemplate == null && c.sprites != null && c.sprites.Length > 0 && !c.name.StartsWith("FCC_"))
                        {
                            fallbackTemplate = c;
                        }

                        if (c.name.StartsWith("Card_A_"))
                        {
                            if (attackTemplate == null || c.name.Contains("Whip") || c.name.Contains("Knife"))
                            {
                                attackTemplate = c;
                            }
                        }
                        else if (c.cardGroup != null && c.cardGroup.IsWeapon)
                        {
                            if (attackTemplate == null) attackTemplate = c;
                        }
                        else if (c.name.StartsWith("Card_D_") || c.name.Contains("Armor"))
                        {
                            if (defenseTemplate == null) defenseTemplate = c;
                        }
                        else if (c.name.StartsWith("Card_S_"))
                        {
                            if (skillTemplate == null) skillTemplate = c;
                        }
                        else if (c.name.StartsWith("Card_W_") || c.name.Contains("Wild") || c.name.Contains("Wings") || c.name.Contains("Bracelet"))
                        {
                            if (wildTemplate == null) wildTemplate = c;
                        }
                        else if (c.name.StartsWith("FCC_"))
                        {
                            if (crawlerTemplate == null) crawlerTemplate = c;
                        }
                    }
                }

                // If fallback is still null, take any available card
                if (fallbackTemplate == null)
                {
                    fallbackTemplate = attackTemplate ?? defenseTemplate ?? skillTemplate ?? wildTemplate ?? crawlerTemplate;
                }

                if (fallbackTemplate == null)
                {
                    LoaderLog.Warn("[CardBridge] No CardConfig template found in memory!");
                    return;
                }

                LoaderLog.Info($"[CardBridge] Discovered templates: Attack='{attackTemplate?.name}', Defense='{defenseTemplate?.name}', Skill='{skillTemplate?.name}', Wild='{wildTemplate?.name}', Crawler='{crawlerTemplate?.name}'");

                CustomCardConfigs.Clear();
                foreach (var def in RegisteredDefs)
                {
                    var color = (def.Color ?? "").ToLowerInvariant();
                    CardConfig template = null;
                    if (color == "attack" || color == "weapon")
                    {
                        template = attackTemplate ?? fallbackTemplate;
                    }
                    else if (color == "defense" || color == "shield" || color == "armor")
                    {
                        template = defenseTemplate ?? attackTemplate ?? fallbackTemplate;
                    }
                    else if (color == "skill" || color == "mana" || color == "support")
                    {
                        template = skillTemplate ?? attackTemplate ?? fallbackTemplate;
                    }
                    else if (color == "wild" || color == "rainbow")
                    {
                        template = wildTemplate ?? skillTemplate ?? fallbackTemplate;
                    }
                    else if (color == "character" || color == "crawler")
                    {
                        template = crawlerTemplate ?? fallbackTemplate;
                    }
                    else
                    {
                        template = attackTemplate ?? fallbackTemplate;
                    }

                    if (template == null) template = fallbackTemplate;

                    var card = UnityEngine.Object.Instantiate(template);
                    card.name = def.Id;
                    card.manaCost = def.Mana;
                    int gemSlots = Math.Clamp(def.GemSlots, 0, 3);
                    card.GemSlotCount = gemSlots;
                    card.MaxGemSlotsAllowed = Math.Max(1, gemSlots);
                    card.addToRewardPool = true;
                    card.allowInTreasureChest = true;
                    card._allowInOfferingTable = true;
                    card.allowInEvoTable = true;
                    card.RewardProbability = def.Weight > 0 ? def.Weight : 100f;
                    card.isEvolvedCard = false;
                    card._maxLevel = 1;
                    card.CardLimit = 99;
                    card._overrideCostForRewardPool = false;
                    card._allowedInDemo = true;
                    card.hideFlags = UnityEngine.HideFlags.DontUnloadUnusedAsset | UnityEngine.HideFlags.HideAndDontSave;

                    // Apply Wild Card mechanics (e.g. Friendship Amulet, Wings)
                    if (color == "wild" || def.Retain || def.FreeToPlay)
                    {
                        if (wildTemplate != null)
                        {
                            if (wildTemplate._cardCostType != null) card._cardCostType = wildTemplate._cardCostType;
                            if (wildTemplate.cardType != null) card.cardType = wildTemplate.cardType;
                        }
                        else
                        {
                            try { card._cardCostType = new Nosebleed.Pancake.GameLogic.WildCostType(); } catch {}
                        }
                        card.ReceivesComboMultiplier = true;
                    }

                    if (def.Retain)
                    {
                        card._hasRetain = true;
                    }

                    if (def.FreeToPlay || def.Mana == 0)
                    {
                        card.FreeToPlay = true;
                        card.manaCost = 0;
                    }

                    // Apply custom sprite / icon if provided
                    if (!string.IsNullOrEmpty(def.IconPath))
                    {
                        try
                        {
                            var customSprite = SpriteLoader.LoadSprite(def.IconPath, def.ModDirectory);
                            if (customSprite != null)
                            {
                                card.sprites = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.Sprite>(new UnityEngine.Sprite[] { customSprite });
                                LoaderLog.Info($"[CardBridge] Assigned custom sprite '{customSprite.name}' to card {card.name} ✓");
                            }
                            else
                            {
                                LoaderLog.Info($"[CardBridge] LoadSprite returned NULL for card {card.name} (iconPath='{def.IconPath}')");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoaderLog.Error($"[CardBridge] Error loading sprite for card {card.name}: {ex}");
                        }
                    }
                    else
                    {
                        LoaderLog.Info($"[CardBridge] Card {def.Id} has no IconPath specified.");
                    }

                    CustomCardConfigs.Add(card);
                    LoaderLog.Info($"[CardBridge] Created CardConfig {def.Id} ('{def.Name}', {def.Color}) from template '{template.name}'");
                }

                _cardsCreated = true;
                LoaderLog.Info($"[CardBridge] Successfully created {CustomCardConfigs.Count} custom cards in-game!");
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardBridge] Failed to create custom cards: {ex}");
            }
        }

        public static CardConfig FindCard(string idOrName)
        {
            EnsureCustomCardsCreated();
            foreach (var c in CustomCardConfigs)
            {
                if (c == null) continue;
                if (string.Equals(c.name, idOrName, StringComparison.OrdinalIgnoreCase) ||
                    c.name.EndsWith(":" + idOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
                var def = FindDef(c);
                if (def != null && string.Equals(def.Name, idOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }
            return null;
        }

        public static CustomCardDef FindDef(CardConfig card)
        {
            if (card == null) return null;
            string cname = card.name;
            if (string.IsNullOrEmpty(cname)) return null;

            string cleanName = cname.Replace("(Clone)", "").Trim();

            return RegisteredDefs.Find(x => 
                string.Equals(x.Id, cleanName, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(x.Name, cleanName, StringComparison.OrdinalIgnoreCase) ||
                cleanName.StartsWith(x.Id, StringComparison.OrdinalIgnoreCase) || 
                cleanName.EndsWith(x.Id, StringComparison.OrdinalIgnoreCase) || 
                cleanName.EndsWith(":" + x.Id, StringComparison.OrdinalIgnoreCase) ||
                cleanName.Contains(x.Id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
