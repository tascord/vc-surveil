using System;
using System.Collections.Generic;
using System.Linq;

namespace PancakeModLoader
{
    public class CustomCrawlerDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> Colors { get; set; } = new();
        public string TriggerWhen { get; set; } = "";
        public string TriggerEffect { get; set; } = "";
        private bool _oncePerTurn = false;
        public bool OncePerTurn { get => _oncePerTurn; set => _oncePerTurn = value; }
        public bool TriggerOncePerTurn { get => _oncePerTurn; set => _oncePerTurn = value; }
        public int BaseHealth { get; set; } = 0;
        public int BaseMana { get; set; } = 0;
        public int BaseMight { get; set; } = 0;
        public string IconPath { get; set; } = "";
        public string ModDirectory { get; set; } = "";
    }

    public static class CrawlerBridge
    {
        public static List<CustomCrawlerDef> RegisteredDefs { get; } = new();

        public static void Register(CustomCrawlerDef def)
        {
            RegisteredDefs.RemoveAll(x => x.Id == def.Id);
            RegisteredDefs.Add(def);
            LoaderLog.Info($"[CrawlerBridge] Registered crawler: {def.Id} ('{def.Name}')");
        }

        public static void RegisterCrawler(CustomCrawlerDef def) => Register(def);

        public static void Patch(string id, Action<CustomCrawlerDef> apply)
        {
            var def = RegisteredDefs.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[CrawlerBridge] Patch target not found: {id}"); return; }
            apply(def);
        }

        public static void PatchCrawler(string id, Action<CustomCrawlerDef> apply) => Patch(id, apply);

        public static CustomCrawlerDef FindDef(string id) => RegisteredDefs.Find(x => x.Id == id);

        public static CustomCrawlerDef GetDef(string id) => FindDef(id);

        public static string[] ListIds() => RegisteredDefs.Select(x => x.Id).ToArray();

        // Native trigger dispatch via hooks pending — demo trigger effects can be
        // observed via hooks.on("cardPlayed"); no native wiring yet.
    }
}
