using System;
using HarmonyLib;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.GameLogic;
using Nosebleed.Pancake.Modal;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.View;
using Nosebleed.Util;

namespace PancakeModLoader
{
    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.GetCardRewardPool))]
    public static class Patch_ChooseCardModal_GetCardRewardPool
    {
        public static void Postfix(Il2CppSystem.Collections.Generic.List<CardConfig> cardRewardPool)
        {
            try
            {
                CardBridge.EnsureCustomCardsCreated();
                foreach (var card in CardBridge.CustomCardConfigs)
                {
                    if (card != null && cardRewardPool != null && !cardRewardPool.Contains(card))
                    {
                        cardRewardPool.Add(card);
                    }
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error injecting cards into reward pool: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Nosebleed.Pancake.InputSystem.InputManager), nameof(Nosebleed.Pancake.InputSystem.InputManager.Update))]
    public static class Patch_InputManager_Update
    {
        public static bool Prefix()
        {
            if (BepInExPlugin.Instance?.Console?.IsOpen == true)
            {
                return false;
            }
            return true;
        }

        public static void Postfix()
        {
            try
            {
                DevConsoleBehaviour.EnsureHostExists();
                DevConsoleBehaviour.CheckToggleInput();
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(PlayerModel), nameof(PlayerModel.InitDeck))]
    public static class Patch_PlayerModel_InitDeck
    {
        public static void Postfix(PlayerModel __instance)
        {
            try
            {
                CardBridge.EnsureCustomCardsCreated();
                foreach (var card in CardBridge.CustomCardConfigs)
                {
                    if (card != null && __instance != null)
                    {
                        __instance.AddCardConfigToDeck(card, false);
                        LoaderLog.Info($"[CardPatches] Added {card.name} to player starting deck!");
                    }
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error adding cards to starting deck: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.OnOpened))]
    public static class Patch_ChooseCardModal_OnOpened
    {
        public static void Postfix(ChooseCardModal __instance)
        {
            try
            {
                if (__instance != null)
                {
                    __instance._inputBlocked = false;
                    LoaderLog.Info("[CardPatches] ChooseCardModal.OnOpened: forced _inputBlocked = false");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error in ChooseCardModal.OnOpened: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.LateUpdate))]
    public static class Patch_ChooseCardModal_LateUpdate
    {
        public static void Postfix(ChooseCardModal __instance)
        {
            try
            {
                if (__instance != null && __instance._inputBlocked)
                {
                    __instance._inputBlocked = false;
                }
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.SetRewardChoices))]
    public static class Patch_ChooseCardModal_SetRewardChoices
    {
        public static void Postfix(ChooseCardModal __instance)
        {
            try
            {
                if (__instance == null) return;
                __instance._inputBlocked = false;

                if (__instance._cardChoiceViews != null)
                {
                    for (int i = 0; i < __instance._cardChoiceViews.Count; i++)
                    {
                        var view = __instance._cardChoiceViews[i];
                        if (view != null)
                        {
                            view.SetButtonsEnabled(true);
                            if (view.BackGroundButton != null)
                            {
                                view.BackGroundButton.interactable = true;
                                view.BackGroundButton.enabled = true;
                            }
                            if (view._choosableCard != null)
                            {
                                view._choosableCard._disablePointerEvents = false;
                            }
                        }
                    }
                }
                LoaderLog.Info("[CardPatches] ChooseCardModal.SetRewardChoices: all choice views unblocked and enabled");
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error in SetRewardChoices: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.AddCard))]
    public static class Patch_ChooseCardModal_AddCard
    {
        public static void Prefix(CardChoiceView cardChoiceView)
        {
            try
            {
                LoaderLog.Info($"[CardPatches] ChooseCardModal.AddCard triggered for: {cardChoiceView?.CardConfig?.name}");
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.CardChosen))]
    public static class Patch_ChooseCardModal_CardChosen
    {
        public static void Prefix(CardView cardView)
        {
            try
            {
                LoaderLog.Info($"[CardPatches] ChooseCardModal.CardChosen triggered for: {cardView?.name}");
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(CardChoiceView), nameof(CardChoiceView.SetReward))]
    public static class Patch_CardChoiceView_SetReward
    {
        public static void Postfix(CardChoiceView __instance, PlayerModel playerModel, CardConfig cardReward, bool isCard, CardConfig evoPreview)
        {
            try
            {
                if (__instance != null)
                {
                    __instance.SetButtonsEnabled(true);
                    if (__instance.BackGroundButton != null)
                    {
                        __instance.BackGroundButton.interactable = true;
                        __instance.BackGroundButton.enabled = true;
                    }
                    if (__instance._choosableCard != null)
                    {
                        __instance._choosableCard._disablePointerEvents = false;
                        if (__instance._choosableCard.CardView != null && cardReward != null)
                        {
                            CardVisualsHelper.Apply(__instance._choosableCard.CardView, cardReward);
                        }
                    }
                    LoaderLog.Info($"[CardPatches] CardChoiceView.SetReward for {cardReward?.name}: buttons enabled ✓");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error in CardChoiceView.SetReward postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CardChoiceView), nameof(CardChoiceView.OnBackgroundButtonPressed))]
    public static class Patch_CardChoiceView_OnBackgroundButtonPressed
    {
        public static void Prefix(CardChoiceView __instance)
        {
            try
            {
                LoaderLog.Info($"[CardPatches] CardChoiceView.OnBackgroundButtonPressed for: {__instance?.name}");
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(ChoosableCard), nameof(ChoosableCard.OnEnable))]
    public static class Patch_ChoosableCard_OnEnable
    {
        public static void Postfix(ChoosableCard __instance)
        {
            try
            {
                if (__instance != null)
                {
                    __instance._disablePointerEvents = false;
                }
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(ChoosableCard), nameof(ChoosableCard.PickCard))]
    public static class Patch_ChoosableCard_PickCard
    {
        public static void Prefix(ChoosableCard __instance)
        {
            try
            {
                LoaderLog.Info($"[CardPatches] ChoosableCard.PickCard for: {__instance?.name}");
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(PointerEvents), nameof(PointerEvents.OnPointerDown))]
    public static class Patch_PointerEvents_OnPointerDown
    {
        public static void Prefix()
        {
            try
            {
                PointerEvents.s_hasPointerDown = false;
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.Name), MethodType.Getter)]
    public static class Patch_CardConfig_get_Name
    {
        public static bool Prefix(CardConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Name;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.GetEffectDescription))]
    public static class Patch_CardConfig_GetEffectDescription
    {
        public static bool Prefix(CardConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Text;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.GetBaseEffectDescription))]
    public static class Patch_CardConfig_GetBaseEffectDescription
    {
        public static bool Prefix(CardConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Text;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.GetLevelUpDescription))]
    public static class Patch_CardConfig_GetLevelUpDescription
    {
        public static bool Prefix(CardConfig __instance, int toLevel, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Text;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.GetMaxLevel))]
    public static class Patch_CardConfig_GetMaxLevel
    {
        public static bool Prefix(CardConfig __instance, ref int __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = 1;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    public static class CardVisualsHelper
    {
        public static void Apply(CardView view, CardConfig cfg)
        {
            if (view == null || cfg == null) return;
            var def = CardBridge.FindDef(cfg);
            if (def == null) return;

            LoaderLog.Info($"[CardVisualsHelper] Applying visuals for card '{cfg.name}' (id={def.Id}): icon='{def.IconPath}', body='{def.BodyPath}'");

            try
            {
                if (!string.IsNullOrEmpty(def.Name) && view._cardNameText != null)
                {
                    view._cardNameText.text = def.Name;
                }
            }
            catch {}

            try
            {
                if (!string.IsNullOrEmpty(def.Text) && view._descriptionText != null)
                {
                    view._descriptionText.text = def.Text;
                }
            }
            catch {}

            // Apply custom icon sprite
            if (!string.IsNullOrEmpty(def.IconPath))
            {
                try
                {
                    var iconSprite = SpriteLoader.LoadSprite(def.IconPath, def.ModDirectory);
                    if (iconSprite != null)
                    {
                        if (view._cardImage != null)
                        {
                            view._cardImage.sprite = iconSprite;
                            view._cardImage.overrideSprite = iconSprite;
                            view._cardImage.type = UnityEngine.UI.Image.Type.Simple;
                            view._cardImage.preserveAspect = true;
                            view._cardImage.enabled = true;
                            view._cardImage.color = UnityEngine.Color.white;
                        }
                        if (view._cardImageShadow != null)
                        {
                            view._cardImageShadow.sprite = iconSprite;
                            view._cardImageShadow.overrideSprite = iconSprite;
                            view._cardImageShadow.type = UnityEngine.UI.Image.Type.Simple;
                            view._cardImageShadow.preserveAspect = true;
                            view._cardImageShadow.enabled = true;
                        }
                        LoaderLog.Info($"[CardVisualsHelper] Assigned icon '{iconSprite.name}' to {view.name} ✓");
                    }
                    else
                    {
                        LoaderLog.Warn($"[CardVisualsHelper] LoadSprite returned null for IconPath '{def.IconPath}'");
                    }
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[CardVisualsHelper] Error applying icon sprite: {ex.Message}");
                }
            }

            // Apply custom card body / background frame
            if (!string.IsNullOrEmpty(def.BodyPath))
            {
                try
                {
                    var bodySprite = SpriteLoader.LoadSprite(def.BodyPath, def.ModDirectory);
                    if (bodySprite != null)
                    {
                        if (view._frontBackground != null)
                        {
                            view._frontBackground.sprite = bodySprite;
                            view._frontBackground.overrideSprite = bodySprite;
                            view._frontBackground.type = UnityEngine.UI.Image.Type.Simple;
                            view._frontBackground.enabled = true;
                            view._frontBackground.color = UnityEngine.Color.white;
                        }
                        if (view._cardTypeBackground != null)
                        {
                            view._cardTypeBackground.enabled = false;
                        }
                        LoaderLog.Info($"[CardVisualsHelper] Assigned body '{bodySprite.name}' to {view.name} ✓");
                    }
                    else
                    {
                        LoaderLog.Warn($"[CardVisualsHelper] LoadSprite returned null for BodyPath '{def.BodyPath}'");
                    }
                }
                catch (Exception ex)
                {
                    LoaderLog.Warn($"[CardVisualsHelper] Error applying body sprite: {ex.Message}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(CardView), nameof(CardView.SetCardConfig), new Type[] { typeof(CardConfig), typeof(bool) })]
    public static class Patch_CardView_SetCardConfig
    {
        public static void Postfix(CardView __instance, CardConfig cardConfig, bool overrideObjName)
        {
            try
            {
                if (__instance == null || cardConfig == null) return;
                CardVisualsHelper.Apply(__instance, cardConfig);
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] SetCardConfig postfix error: {ex}");
            }
        }
    }



    [HarmonyPatch(typeof(FccConfig), nameof(FccConfig.FirstName), MethodType.Getter)]
    public static class Patch_FccConfig_get_FirstName
    {
        public static bool Prefix(FccConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Name;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(FccConfig), nameof(FccConfig.FullName), MethodType.Getter)]
    public static class Patch_FccConfig_get_FullName
    {
        public static bool Prefix(FccConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Name;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(FccConfig), nameof(FccConfig.GetAllActionDescriptions))]
    public static class Patch_FccConfig_GetAllActionDescriptions
    {
        public static bool Prefix(FccConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Text;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    [HarmonyPatch(typeof(FccConfig), nameof(FccConfig.GetFccDescription))]
    public static class Patch_FccConfig_GetFccDescription
    {
        public static bool Prefix(FccConfig __instance, ref string __result)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def != null)
                {
                    __result = def.Text;
                    return false;
                }
            }
            catch {}
            return true;
        }
    }

    public static class CardExecutionHelper
    {
        public static List<T> ToList<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            var result = new List<T>();
            if (list == null) return result;

            int count = -1;
            try
            {
                var asList = list.TryCast<Il2CppSystem.Collections.Generic.List<T>>();
                if (asList != null) count = asList.Count;
            }
            catch {}

            if (count < 0)
            {
                try
                {
                    var asColl = list.TryCast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>();
                    if (asColl != null) count = asColl.Count;
                }
                catch {}
            }

            if (count < 0)
            {
                try
                {
                    var asRefArray = list.TryCast<Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>>();
                    if (asRefArray != null) count = asRefArray.Length;
                }
                catch {}
            }

            if (count >= 0)
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var item = list[i];
                        if (item != null) result.Add(item);
                    }
                    catch { break; }
                }
            }
            else
            {
                // Fallback probe
                for (int i = 0; i < 128; i++)
                {
                    try
                    {
                        var item = list[i];
                        if (item != null) result.Add(item);
                        else break;
                    }
                    catch { break; }
                }
            }

            return result;
        }

        public static List<EnemyModel> GetTargetEnemies(CustomCardDef def, IEncounterModel encounterModel)
        {
            var result = new List<EnemyModel>();
            if (encounterModel == null)
            {
                LoaderLog.Warn("[CardPatches] GetTargetEnemies: encounterModel is null!");
                return result;
            }

            var targetCfg = def.Target ?? new CustomTargetConfig();
            var scope = (targetCfg.Scope ?? "single").ToLowerInvariant();
            var count = targetCfg.Count; // -1 for all

            var allAliveList = new List<EnemyModel>();
            var allAlive = ToList(encounterModel.AliveEnemies);
            foreach (var e in allAlive)
            {
                if (e != null && !e.IsDead && e.Health > 0) allAliveList.Add(e);
            }

            var frontList = new List<EnemyModel>();
            var front = ToList(encounterModel.FrontEnemies);
            foreach (var e in front)
            {
                if (e != null && !e.IsDead && e.Health > 0) frontList.Add(e);
            }

            var groupList = ToList(encounterModel.EnemyGroups);
            var livingRows = new List<List<EnemyModel>>();
            for (int g = 0; g < groupList.Count; g++)
            {
                var group = groupList[g];
                if (group == null || group.Enemies == null) continue;
                var row = new List<EnemyModel>();
                for (int c = 0; c < group.Enemies.Length; c++)
                {
                    var e = group.Enemies[c];
                    if (e != null && !e.IsDead && e.Health > 0) row.Add(e);
                }
                if (row.Count > 0)
                {
                    livingRows.Add(row);
                }
            }

            // Also check RowMovementModel if available
            try
            {
                var eem = encounterModel.TryCast<EnemyEncounterModel>();
                if (livingRows.Count == 0 && eem?.RowMovementModel != null)
                {
                    var rmm = eem.RowMovementModel;
                    var rmmRows = ToList(rmm.EnemyRows);
                    for (int g = 0; g < rmmRows.Count; g++)
                    {
                        var group = rmmRows[g];
                        if (group == null || group.Enemies == null) continue;
                        var row = new List<EnemyModel>();
                        for (int c = 0; c < group.Enemies.Length; c++)
                        {
                            var e = group.Enemies[c];
                            if (e != null && !e.IsDead && e.Health > 0) row.Add(e);
                        }
                        if (row.Count > 0) livingRows.Add(row);
                    }
                }
            }
            catch {}

            LoaderLog.Info($"[CardPatches] GetTargetEnemies: scope='{scope}', targetRow={targetCfg.Row}, targetCol={targetCfg.Column}, count={count}, aliveTotal={allAliveList.Count}, frontCount={frontList.Count}, livingRowsCount={livingRows.Count}");
            for (int r = 0; r < livingRows.Count; r++)
            {
                var names = string.Join(", ", livingRows[r].Select(e => $"{e.EnemyConfig?.name}({e.Health}hp)"));
                LoaderLog.Info($"[CardPatches]   Living Row {r} ({livingRows[r].Count} enemies): [{names}]");
            }

            if (allAliveList.Count == 0)
            {
                LoaderLog.Info("[CardPatches] No alive enemies found in encounter.");
                return result;
            }

            var candidates = new List<EnemyModel>();

            if (scope == "all" || scope == "all_rows" || targetCfg.Row == -3)
            {
                candidates.AddRange(allAliveList);
            }
            else if (scope == "front_row" || targetCfg.Row == 0)
            {
                if (livingRows.Count > 0)
                {
                    candidates.AddRange(livingRows[0]);
                }
                else if (frontList.Count > 0)
                {
                    candidates.AddRange(frontList);
                }
                else
                {
                    candidates.AddRange(allAliveList);
                }
            }
            else if (scope == "back_row" || targetCfg.Row == -2)
            {
                if (livingRows.Count > 0)
                {
                    candidates.AddRange(livingRows[livingRows.Count - 1]);
                }
                else
                {
                    candidates.AddRange(allAliveList);
                }
            }
            else if (scope == "row" || targetCfg.Row >= 0)
            {
                int targetRow = targetCfg.Row >= 0 ? targetCfg.Row : 0;
                if (targetRow < livingRows.Count)
                {
                    candidates.AddRange(livingRows[targetRow]);
                }
                else if (livingRows.Count > 0)
                {
                    candidates.AddRange(livingRows[livingRows.Count - 1]);
                }
                else
                {
                    candidates.AddRange(allAliveList);
                }
            }
            else if (scope == "column" || targetCfg.Column >= 0)
            {
                int targetCol = targetCfg.Column >= 0 ? targetCfg.Column : 0;
                for (int r = 0; r < livingRows.Count; r++)
                {
                    var row = livingRows[r];
                    if (targetCol < row.Count)
                    {
                        candidates.Add(row[targetCol]);
                        if (!targetCfg.Pierce) break;
                    }
                }
                if (candidates.Count == 0) candidates.AddRange(allAliveList);
            }
            else
            {
                // single or default
                if (frontList.Count > 0)
                {
                    candidates.AddRange(frontList);
                }
                else if (livingRows.Count > 0)
                {
                    candidates.AddRange(livingRows[0]);
                }
                else
                {
                    candidates.AddRange(allAliveList);
                }
            }

            // Filtering & sorting modifiers
            if (scope == "prioritize_attackers")
            {
                candidates.Sort((a, b) => (b.CurrentIntent != null).CompareTo(a.CurrentIntent != null));
            }
            else if (scope == "lowest_health")
            {
                candidates.Sort((a, b) => a.Health.CompareTo(b.Health));
            }
            else if (scope == "highest_health")
            {
                candidates.Sort((a, b) => b.Health.CompareTo(a.Health));
            }
            else if (scope == "random" || targetCfg.Random)
            {
                var rng = new System.Random();
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int k = rng.Next(i + 1);
                    var tmp = candidates[k];
                    candidates[k] = candidates[i];
                    candidates[i] = tmp;
                }
            }

            if (scope == "multiple" && count <= 1)
            {
                count = 3;
            }

            if (count == -1 || scope == "all" || scope == "all_rows" || scope == "front_row" || scope == "back_row" || scope == "row")
            {
                if (count > 0 && count < candidates.Count)
                {
                    result.AddRange(candidates.Take(count));
                }
                else
                {
                    result.AddRange(candidates);
                }
            }
            else
            {
                int takeCount = count > 0 ? count : 1;
                result.AddRange(candidates.Take(takeCount));
            }

            LoaderLog.Info($"[CardPatches] Selected {result.Count} targets for scope='{scope}'");
            return result;
        }

        public static void HitEnemy(EnemyModel enemy, double damage, CardModel cardModel)
        {
            if (enemy == null || enemy.IsDead) return;
            enemy.ResolveDamage(damage, cardModel, false, 0, false, "Attack");
            try
            {
                if (enemy.EnemyView != null)
                {
                    enemy.EnemyView.PlayDamageFlash();
                    enemy.EnemyView.DamageHealth(damage, false, false, false);
                }
            }
            catch {}
        }

        public static void FreezeEnemy(EnemyModel enemy, int stacks)
        {
            if (enemy == null || stacks <= 0) return;
            try
            {
                int oldStacks = enemy.FreezeStacks;
                int newStacks = oldStacks + stacks;
                enemy.Freeze(stacks);
                LoaderLog.Info($"[CardPatches] Froze {enemy.EnemyConfig?.name} (+{stacks} stacks, old={oldStacks}, new={newStacks})");

                if (enemy.EnemyView != null)
                {
                    enemy.EnemyView.Freeze(stacks);
                    enemy.EnemyView.RefreshFreeze(oldStacks, newStacks);
                    LoaderLog.Info($"[CardPatches] Refreshed EnemyView freeze visual for {enemy.EnemyConfig?.name} ✓");
                }
                else
                {
                    LoaderLog.Warn($"[CardPatches] enemy.EnemyView was null for {enemy.EnemyConfig?.name}");
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[CardPatches] Failed to freeze enemy: {ex}");
            }
        }

        public static void DisarmEnemy(EnemyModel enemy)
        {
            if (enemy == null || enemy.IsDead) return;
            try
            {
                enemy.RemoveIntentDueToEffect(true);
                LoaderLog.Info($"[CardPatches] Disarmed {enemy.EnemyConfig?.name}!");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[CardPatches] Failed to disarm enemy: {ex.Message}");
            }
        }

        public static void KnockbackEnemy(EnemyModel enemy)
        {
            if (enemy == null || enemy.IsDead) return;
            try
            {
                enemy.OnKnockedBack();
                LoaderLog.Info($"[CardPatches] Knocked back {enemy.EnemyConfig?.name}!");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[CardPatches] Failed to knock back enemy: {ex.Message}");
            }
        }

        public static void EraseEnemy(EnemyModel enemy)
        {
            if (enemy == null || enemy.IsDead) return;
            try
            {
                enemy.Kill();
                LoaderLog.Info($"[CardPatches] Erased {enemy.EnemyConfig?.name}!");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[CardPatches] Failed to erase enemy: {ex.Message}");
            }
        }

        public static void TriggerMinecart(CustomCardDef def, PlayerModel playerModel, IEncounterModel encounterModel, CardModel cardModel, int damageOverride = 0)
        {
            try
            {
                LoaderLog.Info("[CardPatches] Triggering Minecart sweep!");
                try
                {
                    var audioMgr = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Nosebleed.Pancake.Audio.AudioManager>())?.TryCast<Nosebleed.Pancake.Audio.AudioManager>();
                    if (audioMgr != null)
                    {
                        audioMgr.PlaySound("SFX_GAME_Destructable_RailCart_Move_01", 1f, null, 0f, null, null);
                    }
                }
                catch {}

                int dmg = damageOverride > 0 ? damageOverride : (def.Effects?.MinecartDamage > 0 ? def.Effects.MinecartDamage : (def.Damage > 0 ? def.Damage : 25));
                var targets = new List<EnemyModel>();
                var front = ToList(encounterModel?.FrontEnemies);
                foreach (var e in front)
                {
                    if (e != null && !e.IsDead && e.Health > 0) targets.Add(e);
                }
                if (targets.Count == 0)
                {
                    var alive = ToList(encounterModel?.AliveEnemies);
                    foreach (var e in alive)
                    {
                        if (e != null && !e.IsDead && e.Health > 0) targets.Add(e);
                    }
                }

                foreach (var t in targets)
                {
                    HitEnemy(t, dmg, cardModel);
                    KnockbackEnemy(t);
                }
                LoaderLog.Info($"[CardPatches] Minecart swept across {targets.Count} enemies dealing {dmg} damage + knockback!");
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error triggering minecart: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CardConfig), nameof(CardConfig.OnPlayCard))]
    public static class Patch_CardConfig_OnPlayCard
    {
        public static void Postfix(CardConfig __instance, GameModel gameModel, PlayerModel playerModel, CardModel cardModel, IEncounterModel encounterModel, Nosebleed.Pancake.GameLogic.PlayerStats.StatSnapshot playerStats, bool useCommandQueue)
        {
            try
            {
                var def = CardBridge.FindDef(__instance);
                if (def == null) return;

                // Fallback encounter model resolution if passed null
                if (encounterModel == null)
                {
                    try
                    {
                        var eem = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<EnemyEncounterModel>())?.TryCast<EnemyEncounterModel>();
                        if (eem != null) encounterModel = eem.Cast<IEncounterModel>();
                    }
                    catch {}
                }

                if (playerModel == null)
                {
                    try
                    {
                        playerModel = gameModel?.PlayerModel ?? UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<PlayerModel>())?.TryCast<PlayerModel>();
                    }
                    catch {}
                }

                int damage = def.Damage > 0 ? def.Damage : (def.Effects != null ? def.Effects.Damage : 0);
                LoaderLog.Info($"[CardPatches] OnPlayCard: Executing custom card {def.Name} (id={def.Id}, damage={damage}, freeze={def.Effects?.Freeze}, disarm={def.Effects?.Disarm}, knockback={def.Effects?.Knockback}, scope={def.Target?.Scope})");

                var targets = CardExecutionHelper.GetTargetEnemies(def, encounterModel);

                if (targets.Count > 0)
                {
                    foreach (var target in targets)
                    {
                        string enemyName = target.EnemyConfig?.name ?? "enemy";

                        // Apply status effects (freeze, disarm, knockback) before lethal damage
                        if (def.Effects != null)
                        {
                            if (def.Effects.Freeze > 0)
                            {
                                CardExecutionHelper.FreezeEnemy(target, def.Effects.Freeze);
                            }
                            if (def.Effects.Disarm)
                            {
                                CardExecutionHelper.DisarmEnemy(target);
                            }
                            if (def.Effects.Knockback)
                            {
                                CardExecutionHelper.KnockbackEnemy(target);
                            }
                        }

                        if (damage > 0)
                        {
                            CardExecutionHelper.HitEnemy(target, damage, cardModel);
                            LoaderLog.Info($"[CardPatches] Custom card {def.Name} dealt {damage} damage to {enemyName}! Remaining HP: {target.Health}");
                        }

                        if (def.Effects != null && def.Effects.Erase)
                        {
                            CardExecutionHelper.EraseEnemy(target);
                        }
                    }
                }

                if (def.Effects != null && def.Effects.Minecart)
                {
                    CardExecutionHelper.TriggerMinecart(def, playerModel, encounterModel, cardModel);
                }

                if (def.Effects != null && !string.IsNullOrEmpty(def.Effects.Sound))
                {
                    try
                    {
                        var audioMgr = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<Nosebleed.Pancake.Audio.AudioManager>())?.TryCast<Nosebleed.Pancake.Audio.AudioManager>();
                        audioMgr?.PlaySound(def.Effects.Sound, 1f, null, 0f, null, null);
                    }
                    catch {}
                }

                int armor = def.Armor > 0 ? def.Armor : (def.Effects != null ? def.Effects.Armor : 0);
                if (armor > 0 && playerModel != null)
                {
                    playerModel.TryAddArmor(armor, true);
                    LoaderLog.Info($"[CardPatches] Custom card {def.Name} added {armor} Armor!");
                }

                if (def.Effects != null && playerModel != null)
                {
                    if (def.Effects.Heal > 0)
                    {
                        playerModel.PlayerStats?.Health?.AddValue((float)def.Effects.Heal, true);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} healed player for {def.Effects.Heal} HP!");
                    }

                    if (def.Effects.Mana > 0)
                    {
                        playerModel.TryAddMana(def.Effects.Mana, false, true);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} added {def.Effects.Mana} Mana!");
                    }

                    if (def.Effects.Draw > 0)
                    {
                        playerModel.DrawCards(def.Effects.Draw, encounterModel);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} drew {def.Effects.Draw} cards!");
                    }

                    if (def.Effects.Might > 0)
                    {
                        playerModel.PlayerStats?.Might?.AddValue((float)def.Effects.Might, true);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} increased player Might by {def.Effects.Might}!");
                    }

                    if (def.Effects.Coins > 0)
                    {
                        playerModel.AddCoins(def.Effects.Coins, true);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} added {def.Effects.Coins} coins!");
                    }

                    if (def.Effects.Xp > 0)
                    {
                        playerModel.AddXp(def.Effects.Xp, true);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} added {def.Effects.Xp} XP!");
                    }

                    if (def.Effects.Combo != 0 && playerModel.CardCombo != null)
                    {
                        playerModel.CardCombo.Value = Math.Max(0, playerModel.CardCombo.Value + def.Effects.Combo);
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} modified combo by {def.Effects.Combo}! New combo: {playerModel.CardCombo.Value}");
                    }

                    if (def.Effects.ReverseCombo)
                    {
                        playerModel.SetComboReverse();
                        LoaderLog.Info($"[CardPatches] Custom card {def.Name} reversed combo order!");
                    }
                }

                if (def.OnPlayAction != null && playerModel != null)
                {
                    def.OnPlayAction(playerModel, cardModel, encounterModel);
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[CardPatches] Error in OnPlayCard: {ex}");
            }
        }
    }
}
