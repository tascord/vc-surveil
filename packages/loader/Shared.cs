using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;
using Jint.Native;
using Jint.Native.Object;


namespace PancakeModLoader
{
    public record ModManifest(string Id, string Version, string Entry, string? Name = null);
    public record DiscoveredMod(ModManifest Manifest, string RootDir, string EntryPath);

    public class ModDiscovery
    {
        private readonly string _primaryRoot;
        private readonly string? _fallbackRoot;
        public ModDiscovery(string primaryRoot, string? fallbackRoot = null)
        {
            _primaryRoot = primaryRoot;
            _fallbackRoot = fallbackRoot;
        }
        private IEnumerable<string> Roots()
        {
            if (Directory.Exists(_primaryRoot)) yield return _primaryRoot;
            else LoaderLog.Info($"Primary mods missing (will create): {_primaryRoot}");
            if (_fallbackRoot != null && _fallbackRoot != _primaryRoot && Directory.Exists(_fallbackRoot))
                yield return _fallbackRoot;
            var cwdMods = Path.Combine(Directory.GetCurrentDirectory(), ".mods");
            if (Directory.Exists(cwdMods) && cwdMods != _primaryRoot && cwdMods != _fallbackRoot) yield return cwdMods;
        }
        public List<DiscoveredMod> Discover()
        {
            var list = new List<DiscoveredMod>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in Roots())
            {
                LoaderLog.Info($"Scanning mods in {root}");
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(dir, "mod.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var id = ExtractJson(json, "id") ?? Path.GetFileName(dir);
                        if (seen.Contains(id)) { LoaderLog.Info($"Skipping duplicate mod {id} from {root}"); continue; }
                        var version = ExtractJson(json, "version") ?? "0.0.0";
                        var entry = ExtractJson(json, "entry") ?? "dist/mod.js";
                        var manifest = new ModManifest(id, version, entry);
                        var entryPath = Path.Combine(dir, entry);
                        if (!File.Exists(entryPath))
                        {
                            var alt = Path.Combine(dir, "dist/index.js");
                            if (File.Exists(alt)) entryPath = alt;
                        }
                        if (File.Exists(entryPath))
                        {
                            list.Add(new DiscoveredMod(manifest, dir, entryPath));
                            seen.Add(id);
                        }
                        else LoaderLog.Warn($"mod {id}: entry not found {entryPath}");
                    }
                    catch (Exception ex) { LoaderLog.Warn($"bad manifest {manifestPath}: {ex.Message}"); }
                }
            }
            return list;
        }
        private static string? ExtractJson(string json, string key)
        {
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            var colon = json.IndexOf(':', idx);
            if (colon < 0) return null;
            var start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            var end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }
    }

    public class JsRuntime
    {
        private readonly DevConsole _console;

        public JsRuntime(DevConsole console = null)
        {
            _console = console;
        }

        public void LoadMod(DiscoveredMod mod)
        {
            var code = File.ReadAllText(mod.EntryPath);
            LoaderLog.Info($"[JsRuntime] Loading {mod.Manifest.Id} ({code.Length} bytes) from {mod.EntryPath}");

            // 1. Try Jint execution
            try
            {
                var engine = new Jint.Engine();

                engine.SetValue("__log", new Action<string>(msg => LoaderLog.Info($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__warn", new Action<string>(msg => LoaderLog.Warn($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__error", new Action<string>(msg => LoaderLog.Error($"[{mod.Manifest.Id}] {msg}")));
                engine.SetValue("__registerCard", new Action<JsValue>(val => RegisterCardFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__registerArcana", new Action<JsValue>(val => RegisterArcanaFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__registerEnemy", new Action<JsValue>(val => RegisterEnemyFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__registerAudio", new Action<JsValue>(val => RegisterAudioFromJs(val, mod.Manifest.Id, mod.RootDir, engine)));
                engine.SetValue("__playAudio", new Action<string>(val => AudioBridge.PlayAudio(val)));
                engine.SetValue("__consoleRegister", new Action<string, string, JsValue>((name, desc, fn) =>
                {
                    _console?.Register(name, desc, (args, c) =>
                    {
                        try
                        {
                            if (fn != null && !fn.IsUndefined() && !fn.IsNull())
                            {
                                var ctx = new { log = new Action<string>(msg => c.WriteLine(msg)) };
                                engine.Invoke(fn, args, ctx);
                            }
                        }
                        catch (Exception ex)
                        {
                            c.WriteLine($"command error: {ex.Message}");
                        }
                    });
                }));

                engine.Execute(@"
const console = {
    log: (...args) => __log(args.join(' ')),
    warn: (...args) => __warn(args.join(' ')),
    error: (...args) => __error(args.join(' '))
};
globalThis.vc = {
    cards: {
        register: (c) => __registerCard(c),
        patch: () => {},
        list: () => []
    },
    arcanas: {
        register: (c) => __registerArcana(c)
    },
    enemies: {
        register: (c) => __registerEnemy(c)
    },
    audio: {
        register: (c) => __registerAudio(c),
        play: (id) => __playAudio(id)
    },
    hooks: {
        on: (ev, cb) => __log('hook registered: ' + ev)
    },
    console: {
        register: (n, d, f) => __consoleRegister(n, d, f),
        log: (...args) => __log(args.join(' '))
    },
    log: (...args) => __log(args.join(' '))
};
");

                var scriptCode = System.Text.RegularExpressions.Regex.Replace(code, @"\bexport\s+", "");
                engine.Execute(scriptCode);
                LoaderLog.Info($"[JsRuntime] Executed {mod.Manifest.Id} in Jint successfully ✓");
            }
            catch (Exception ex)
            {
                LoaderLog.Warn($"[JsRuntime] Jint note: {ex.Message} — running fallback card parser.");
                // Fallback parser ensuring registered cards are detected if JS runtime failed
                FallbackParseCards(code, mod.Manifest.Id, mod.RootDir);
            }
        }

        private void RegisterCardFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;

            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var nameVal = obj.Get("name");
            var name = nameVal.IsString() ? nameVal.AsString() : id;

            var textVal = obj.Get("text");
            var text = textVal.IsString() ? textVal.AsString() : "";

            var colorVal = obj.Get("color");
            var color = colorVal.IsString() ? colorVal.AsString() : "attack";

            var rarityVal = obj.Get("rarity");
            var rarity = rarityVal.IsString() ? rarityVal.AsString() : "Common";

            var manaVal = obj.Get("mana");
            var mana = 1;
            if (manaVal.IsNumber()) mana = (int)manaVal.AsNumber();
            else if (manaVal.IsString() && manaVal.AsString() == "W") mana = 0;

            var damageVal = obj.Get("damage");
            var damage = damageVal.IsNumber() ? (int)damageVal.AsNumber() : 0;

            var armorVal = obj.Get("armor");
            var armor = armorVal.IsNumber() ? (int)armorVal.AsNumber() : 0;

            var gemSlotsVal = obj.Get("gemSlots");
            var gemSlots = gemSlotsVal.IsNumber() ? Math.Clamp((int)gemSlotsVal.AsNumber(), 0, 3) : 1;

            var weightVal = obj.Get("weight");
            var weight = weightVal.IsNumber() ? (int)weightVal.AsNumber() : 100;

            var pools = new List<string>();
            var dropVal = obj.Get("drop");
            if (dropVal is ObjectInstance dropObj)
            {
                var poolsVal = dropObj.Get("pools");
                if (poolsVal.IsArray())
                {
                    foreach (var elem in poolsVal.AsArray())
                    {
                        if (elem.IsString()) pools.Add(elem.AsString());
                    }
                }
            }
            if (pools.Count == 0) pools.Add("levelUp");

            var keywords = new List<string>();
            var kwVal = obj.Get("keywords");
            if (kwVal.IsArray())
            {
                foreach (var k in kwVal.AsArray())
                {
                    if (k.IsString()) keywords.Add(k.AsString());
                }
            }

            var target = new CustomTargetConfig();
            var targetVal = obj.Get("target");
            if (targetVal is ObjectInstance targetObj)
            {
                var scopeVal = targetObj.Get("scope");
                if (scopeVal.IsString()) target.Scope = scopeVal.AsString();

                var countVal = targetObj.Get("count");
                if (countVal.IsNumber()) target.Count = (int)countVal.AsNumber();
                else if (countVal.IsString() && countVal.AsString().Equals("all", StringComparison.OrdinalIgnoreCase)) target.Count = -1;

                var rowVal = targetObj.Get("row");
                if (rowVal.IsNumber()) target.Row = (int)rowVal.AsNumber();
                else if (rowVal.IsString())
                {
                    var rStr = rowVal.AsString().ToLowerInvariant();
                    if (rStr == "front") target.Row = 0;
                    else if (rStr == "back") target.Row = -2;
                    else if (rStr == "all") target.Row = -3;
                }

                var colVal = targetObj.Get("column");
                if (colVal.IsNumber()) target.Column = (int)colVal.AsNumber();

                var pierceVal = targetObj.Get("pierce");
                if (pierceVal.IsBoolean()) target.Pierce = pierceVal.AsBoolean();
                else if (pierceVal.IsNumber()) target.Pierce = pierceVal.AsNumber() > 0;

                var randVal = targetObj.Get("random");
                if (randVal.IsBoolean()) target.Random = randVal.AsBoolean();
            }

            var effects = new CustomEffectsConfig();
            var fxVal = obj.Get("effects");
            if (fxVal is ObjectInstance fxObj)
            {
                var freezeVal = fxObj.Get("freeze");
                if (freezeVal.IsNumber()) effects.Freeze = (int)freezeVal.AsNumber();

                var kbVal = fxObj.Get("knockback");
                if (kbVal.IsBoolean()) effects.Knockback = kbVal.AsBoolean();
                else if (kbVal.IsNumber()) effects.Knockback = kbVal.AsNumber() > 0;

                var dmgVal = fxObj.Get("damage");
                if (dmgVal.IsNumber()) effects.Damage = (int)dmgVal.AsNumber();

                var armVal = fxObj.Get("armor");
                if (armVal.IsNumber()) effects.Armor = (int)armVal.AsNumber();

                var healVal = fxObj.Get("heal");
                if (healVal.IsNumber()) effects.Heal = (int)healVal.AsNumber();

                var manaFxVal = fxObj.Get("mana");
                if (manaFxVal.IsNumber()) effects.Mana = (int)manaFxVal.AsNumber();

                var drawVal = fxObj.Get("draw");
                if (drawVal.IsNumber()) effects.Draw = (int)drawVal.AsNumber();

                var mightVal = fxObj.Get("might");
                if (mightVal.IsNumber()) effects.Might = (int)mightVal.AsNumber();

                var cartVal = fxObj.Get("minecart");
                if (cartVal.IsBoolean()) effects.Minecart = cartVal.AsBoolean();
                else if (cartVal is ObjectInstance cartObj)
                {
                    effects.Minecart = true;
                    var cartDmg = cartObj.Get("damage");
                    if (cartDmg.IsNumber()) effects.MinecartDamage = (int)cartDmg.AsNumber();
                }

                var disarmVal = fxObj.Get("disarm");
                if (disarmVal.IsBoolean()) effects.Disarm = disarmVal.AsBoolean();
                else if (disarmVal.IsNumber()) effects.Disarm = disarmVal.AsNumber() > 0;

                var eraseVal = fxObj.Get("erase");
                if (eraseVal.IsBoolean()) effects.Erase = eraseVal.AsBoolean();
                else if (eraseVal.IsNumber()) effects.Erase = eraseVal.AsNumber() > 0;

                var coinsVal = fxObj.Get("coins");
                if (coinsVal.IsNumber()) effects.Coins = (int)coinsVal.AsNumber();

                var xpVal = fxObj.Get("xp");
                if (xpVal.IsNumber()) effects.Xp = (int)xpVal.AsNumber();

                var comboVal = fxObj.Get("combo");
                if (comboVal.IsNumber()) effects.Combo = (int)comboVal.AsNumber();

                var revVal = fxObj.Get("reverseCombo");
                if (revVal.IsBoolean()) effects.ReverseCombo = revVal.AsBoolean();

                var sndVal = fxObj.Get("sound");
                if (sndVal.IsString()) effects.Sound = sndVal.AsString();
            }

            var retainVal = obj.Get("retain");
            var retain = retainVal.IsBoolean() && retainVal.AsBoolean();

            var ftpVal = obj.Get("freeToPlay");
            var freeToPlay = ftpVal.IsBoolean() && ftpVal.AsBoolean();

            var iconPath = "";
            var iconVal = obj.Get("icon");
            if (iconVal.IsString()) iconPath = iconVal.AsString();
            else
            {
                var ipVal = obj.Get("iconPath");
                if (ipVal.IsString()) iconPath = ipVal.AsString();
                else
                {
                    var spVal = obj.Get("sprite");
                    if (spVal.IsString()) iconPath = spVal.AsString();
                }
            }

            var bodyPath = "";
            var bodyVal = obj.Get("body");
            if (bodyVal.IsString()) bodyPath = bodyVal.AsString();
            else
            {
                var bpVal = obj.Get("bodyPath");
                if (bpVal.IsString()) bodyPath = bpVal.AsString();
                else
                {
                    var bgVal = obj.Get("background");
                    if (bgVal.IsString()) bodyPath = bgVal.AsString();
                }
            }

            if (effects.Freeze == 0 && keywords.Exists(k => k.Equals("Freeze", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Freeze = 1;
            }
            if (!effects.Knockback && keywords.Exists(k => k.Equals("Knockback", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Knockback = true;
            }
            if (!effects.Disarm && keywords.Exists(k => k.Equals("Disarm", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Disarm = true;
            }
            if (!effects.Erase && keywords.Exists(k => k.Equals("Erase", StringComparison.OrdinalIgnoreCase)))
            {
                effects.Erase = true;
            }

            var def = new CustomCardDef
            {
                Id = id,
                Name = name,
                Text = text,
                Color = color,
                Mana = mana,
                Damage = damage,
                Armor = armor,
                GemSlots = gemSlots,
                Retain = retain,
                FreeToPlay = freeToPlay,
                IconPath = iconPath,
                BodyPath = bodyPath,
                ModDirectory = modDir,
                Rarity = rarity,
                Weight = weight,
                Pools = pools,
                Keywords = keywords,
                Target = target,
                Effects = effects
            };

            var onPlayVal = obj.Get("onPlay");
            Action<Nosebleed.Pancake.Models.PlayerModel, Nosebleed.Pancake.Models.CardModel, Nosebleed.Pancake.Models.IEncounterModel> onPlayAction = null;
            if (onPlayVal != null && !onPlayVal.IsUndefined() && !onPlayVal.IsNull())
            {
                onPlayAction = (player, cardModel, encounterModel) =>
                {
                    try
                    {
                        var ctx = engine.Evaluate("({})").AsObject();
                        ctx.Set("playerMana", JsValue.FromObject(engine, player.CachedMana));
                        ctx.Set("combo", JsValue.FromObject(engine, player.ComboMultiplier));
                        ctx.Set("damageMultiplier", JsValue.FromObject(engine, 1.0));
                        ctx.Set("addMana", JsValue.FromObject(engine, new Action<int>(n => player.TryAddMana(n, false, true))));
                        ctx.Set("addArmor", JsValue.FromObject(engine, new Action<int>(n => player.TryAddArmor(n, true))));
                        ctx.Set("heal", JsValue.FromObject(engine, new Action<int>(n => player.PlayerStats?.Health?.AddValue((float)n, true))));
                        ctx.Set("draw", JsValue.FromObject(engine, new Action<int>(n => player.DrawCards(n, encounterModel))));
                        ctx.Set("dealDamage", JsValue.FromObject(engine, new Action<int>(n => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.HitEnemy(t, n, cardModel);
                        })));
                        ctx.Set("freeze", JsValue.FromObject(engine, new Action<int>(stacks => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.FreezeEnemy(t, stacks > 0 ? stacks : 1);
                        })));
                        ctx.Set("knockback", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.KnockbackEnemy(t);
                        })));
                        ctx.Set("disarm", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.DisarmEnemy(t);
                        })));
                        ctx.Set("erase", JsValue.FromObject(engine, new Action(() => {
                            var tgts = CardExecutionHelper.GetTargetEnemies(def, encounterModel);
                            foreach (var t in tgts) CardExecutionHelper.EraseEnemy(t);
                        })));
                        ctx.Set("addCoins", JsValue.FromObject(engine, new Action<int>(n => player.AddCoins(n, true))));
                        ctx.Set("addXp", JsValue.FromObject(engine, new Action<int>(n => player.AddXp(n, true))));
                        ctx.Set("addCombo", JsValue.FromObject(engine, new Action<int>(n => {
                            if (player.CardCombo != null) player.CardCombo.Value = Math.Max(0, player.CardCombo.Value + n);
                        })));
                        ctx.Set("getCombo", JsValue.FromObject(engine, new Func<int>(() => player.CardCombo != null ? player.CardCombo.Value : 0)));
                        ctx.Set("reverseCombo", JsValue.FromObject(engine, new Action(() => player.SetComboReverse())));
                        ctx.Set("minecart", JsValue.FromObject(engine, new Action(() => {
                            CardExecutionHelper.TriggerMinecart(def, player, encounterModel, cardModel);
                        })));

                        engine.Invoke(onPlayVal, ctx);
                    }
                    catch (Exception ex)
                    {
                        LoaderLog.Error($"[JsRuntime] Error executing onPlay for card {id}: {ex.Message}");
                    }
                };
            }
            def.OnPlayAction = onPlayAction;

            CardBridge.RegisterCard(def);
        }

        private void RegisterArcanaFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;

            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var nameVal = obj.Get("name");
            var name = nameVal.IsString() ? nameVal.AsString() : id;

            var textVal = obj.Get("text");
            var text = textVal.IsString() ? textVal.AsString() : "";

            var descVal = obj.Get("description");
            var desc = descVal.IsString() ? descVal.AsString() : "";

            var iconPath = "";
            var iconVal = obj.Get("icon");
            if (iconVal.IsString()) iconPath = iconVal.AsString();
            else
            {
                var ipVal = obj.Get("iconPath");
                if (ipVal.IsString()) iconPath = ipVal.AsString();
            }

            /*
            var def = new CustomArcanaDef
            {
                Id = id,
                Name = name,
                Text = text,
                Description = desc,
                IconPath = iconPath,
                ModDirectory = modDir
            };

            ArcanaBridge.RegisterArcana(def);
            */
        }

        private void FallbackParseCards(string code, string defaultModId, string modDir)
        {
            try
            {
                var idMatches = System.Text.RegularExpressions.Regex.Matches(code, @"id\s*:\s*""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match idM in idMatches)
                {
                    var id = idM.Groups[1].Value;
                    if (string.IsNullOrEmpty(id) || id == defaultModId || !id.Contains(":") || CardBridge.RegisteredDefs.Exists(x => x.Id == id)) continue;

                    var idx = idM.Index;
                    var snippet = code.Substring(Math.Max(0, idx - 50), Math.Min(900, code.Length - Math.Max(0, idx - 50)));

                    var nameM = System.Text.RegularExpressions.Regex.Match(snippet, @"name\s*:\s*""([^""]+)""");
                    var textM = System.Text.RegularExpressions.Regex.Match(snippet, @"text\s*:\s*""([^""]+)""");
                    var colorM = System.Text.RegularExpressions.Regex.Match(snippet, @"color\s*:\s*""([^""]+)""");
                    var manaM = System.Text.RegularExpressions.Regex.Match(snippet, @"mana\s*:\s*(\d+)");
                    var damageM = System.Text.RegularExpressions.Regex.Match(snippet, @"damage\s*:\s*(\d+)");
                    var armorM = System.Text.RegularExpressions.Regex.Match(snippet, @"armor\s*:\s*(\d+)");
                    var gemSlotsM = System.Text.RegularExpressions.Regex.Match(snippet, @"gemSlots\s*:\s*(\d+)");
                    var weightM = System.Text.RegularExpressions.Regex.Match(snippet, @"weight\s*:\s*(\d+)");
                    var rarityM = System.Text.RegularExpressions.Regex.Match(snippet, @"rarity\s*:\s*""([^""]+)""");

                    var target = new CustomTargetConfig();
                    var scopeM = System.Text.RegularExpressions.Regex.Match(snippet, @"scope\s*:\s*""([^""]+)""");
                    if (scopeM.Success) target.Scope = scopeM.Groups[1].Value;
                    var countM = System.Text.RegularExpressions.Regex.Match(snippet, @"count\s*:\s*(\d+)");
                    if (countM.Success) target.Count = int.Parse(countM.Groups[1].Value);

                    var effects = new CustomEffectsConfig();
                    var freezeM = System.Text.RegularExpressions.Regex.Match(snippet, @"freeze\s*:\s*(\d+)");
                    if (freezeM.Success) effects.Freeze = int.Parse(freezeM.Groups[1].Value);
                    if (snippet.Contains("knockback: true") || snippet.Contains("Knockback")) effects.Knockback = true;
                    if (snippet.Contains("disarm: true") || snippet.Contains("Disarm")) effects.Disarm = true;
                    if (snippet.Contains("erase: true") || snippet.Contains("Erase")) effects.Erase = true;
                    if (snippet.Contains("minecart: true") || snippet.Contains("minecart")) effects.Minecart = true;
                    var healM = System.Text.RegularExpressions.Regex.Match(snippet, @"heal\s*:\s*(\d+)");
                    if (healM.Success) effects.Heal = int.Parse(healM.Groups[1].Value);
                    var coinsM = System.Text.RegularExpressions.Regex.Match(snippet, @"coins\s*:\s*(\d+)");
                    if (coinsM.Success) effects.Coins = int.Parse(coinsM.Groups[1].Value);
                    var xpM = System.Text.RegularExpressions.Regex.Match(snippet, @"xp\s*:\s*(\d+)");
                    if (xpM.Success) effects.Xp = int.Parse(xpM.Groups[1].Value);
                    var comboM = System.Text.RegularExpressions.Regex.Match(snippet, @"combo\s*:\s*(\d+)");
                    if (comboM.Success) effects.Combo = int.Parse(comboM.Groups[1].Value);
                    if (snippet.Contains("reverseCombo: true") || snippet.Contains("reverseCombo:true")) effects.ReverseCombo = true;

                    var iconM = System.Text.RegularExpressions.Regex.Match(snippet, @"(?:icon|iconPath|sprite)\s*:\s*""([^""]+)""");
                    var bodyM = System.Text.RegularExpressions.Regex.Match(snippet, @"(?:body|bodyPath|background)\s*:\s*""([^""]+)""");
                    var def = new CustomCardDef
                    {
                        Id = id,
                        Name = nameM.Success ? nameM.Groups[1].Value : id,
                        Text = textM.Success ? textM.Groups[1].Value : "",
                        Color = colorM.Success ? colorM.Groups[1].Value : "attack",
                        Mana = manaM.Success ? int.Parse(manaM.Groups[1].Value) : 1,
                        Damage = damageM.Success ? int.Parse(damageM.Groups[1].Value) : 0,
                        Armor = armorM.Success ? int.Parse(armorM.Groups[1].Value) : 0,
                        GemSlots = gemSlotsM.Success ? Math.Clamp(int.Parse(gemSlotsM.Groups[1].Value), 0, 3) : 1,
                        Retain = snippet.Contains("retain: true"),
                        FreeToPlay = snippet.Contains("freeToPlay: true"),
                        IconPath = iconM.Success ? iconM.Groups[1].Value : "",
                        BodyPath = bodyM.Success ? bodyM.Groups[1].Value : "",
                        ModDirectory = modDir,
                        Rarity = rarityM.Success ? rarityM.Groups[1].Value : "Common",
                        Weight = weightM.Success ? int.Parse(weightM.Groups[1].Value) : 100,
                        Pools = new List<string> { "levelUp", "chestT1" },
                        Target = target,
                        Effects = effects
                    };

                    CardBridge.RegisterCard(def);
                }
            }
            catch { }
        }

        private void RegisterEnemyFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var id = idVal.AsString();
            if (string.IsNullOrEmpty(id)) return;

            var def = new CustomEnemyDef { Id = id, ModDirectory = modDir };
            var nameVal = obj.Get("name");
            if (nameVal.IsString()) def.Name = nameVal.AsString();

            var healthVal = obj.Get("health");
            if (healthVal.IsNumber()) def.Health = (int)healthVal.AsNumber();

            var xpVal = obj.Get("xp");
            if (xpVal.IsNumber()) def.Xp = (int)xpVal.AsNumber();

            var bossVal = obj.Get("boss");
            if (bossVal.IsBoolean()) def.Boss = bossVal.AsBoolean();

            var maxHitVal = obj.Get("maxHit");
            if (maxHitVal.IsNumber()) def.MaxHit = (int)maxHitVal.AsNumber();

            var maxArmorVal = obj.Get("maxArmor");
            if (maxArmorVal.IsNumber()) def.MaxArmor = (int)maxArmorVal.AsNumber();

            var spritePathVal = obj.Get("spritePath");
            if (spritePathVal.IsString()) def.SpritePath = spritePathVal.AsString();

            var framesVal = obj.Get("frames");
            if (framesVal.IsNumber()) def.Frames = (int)framesVal.AsNumber();

            var fpsVal = obj.Get("fps");
            if (fpsVal.IsNumber()) def.Fps = (int)fpsVal.AsNumber();

            EnemyBridge.RegisterEnemy(def);
        }

        private void RegisterAudioFromJs(JsValue val, string defaultModId, string modDir, Engine engine)
        {
            if (val is not ObjectInstance obj) return;
            var idVal = obj.Get("id");
            if (!idVal.IsString()) return;
            var pathVal = obj.Get("path");
            if (!pathVal.IsString()) return;

            var def = new AudioDef
            {
                Id = idVal.AsString(),
                Path = pathVal.AsString(),
                ModDirectory = modDir
            };
            AudioBridge.RegisterAudio(def);
        }
    }

    internal static class ModRegistry
    {
        public static List<string> LoadedIds { get; } = new();
        public static List<(string Id, string? Rarity, int? Weight, List<string>? Pools)> RegisteredCards { get; } = new();
        public static void NoteLoaded(string id) { if (!LoadedIds.Contains(id)) LoadedIds.Add(id); }
        public static void NoteCard(string id, string? rarity, int? weight, List<string>? pools) => RegisteredCards.Add((id, rarity, weight, pools));
    }

    internal static class LoaderLog
    {
        public static void Info(string msg) => Console.WriteLine($"[PancakeModLoader] {msg}");
        public static void Warn(string msg) => Console.WriteLine($"[PancakeModLoader:WARN] {msg}");
        public static void Error(string msg) => Console.Error.WriteLine($"[PancakeModLoader:ERR] {msg}");
    }
}
