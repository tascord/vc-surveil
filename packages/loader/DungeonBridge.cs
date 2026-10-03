using System;
using System.Collections.Generic;
using System.Linq;

namespace PancakeModLoader
{
    public class BiomeEncounterEntry
    {
        public string EnemyId { get; set; } = "";
        public float Weight { get; set; } = 0;
        public List<int> Floors { get; set; } = new();
    }

    public class CustomBiomeDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Floors { get; set; } = 0;
        public int Difficulty { get; set; } = 0;
        public string Background { get; set; } = "";
        public string Music { get; set; } = "";
        public List<BiomeEncounterEntry> EncounterTable { get; set; } = new();
        public int BossFloor { get; set; } = 0;
        public Dictionary<string, object> Modifiers { get; set; } = new();
        public string ModDirectory { get; set; } = "";
    }

    public class CustomRoomReward
    {
        public int Coins { get; set; } = 0;
        public int Xp { get; set; } = 0;
        public List<string> Cards { get; set; } = new();
    }

    public class CustomRoomTemplate
    {
        public string Id { get; set; } = "";
        public int Weight { get; set; } = 100;
        public List<string> Tags { get; set; } = new();
        public string BiomeId { get; set; } = "";
        public int MinFloor { get; set; } = 0;
        public int MaxFloor { get; set; } = 0;
        public List<string> Enemies { get; set; } = new();
        public CustomRoomReward Reward { get; set; } = new();
        public string ModDirectory { get; set; } = "";
    }

    /// Declarative biome/room store.
    /// Native room-gen hook pending assembly anchor — defs are kept for
    /// `vc.dungeons` round-trips; no native floor/map generation injection is wired yet.
    public static class DungeonBridge
    {
        public static List<CustomBiomeDef> Biomes { get; } = new();
        public static List<CustomRoomTemplate> Rooms { get; } = new();

        public static void RegisterBiome(CustomBiomeDef def)
        {
            Biomes.RemoveAll(x => x.Id == def.Id);
            Biomes.Add(def);
            LoaderLog.Info($"[DungeonBridge] Registered biome: {def.Id} ('{def.Name}') — declarative, native room-gen hook pending assembly anchor");
        }

        public static void PatchBiome(string id, Action<CustomBiomeDef> apply)
        {
            var def = Biomes.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[DungeonBridge] Patch biome target not found: {id}"); return; }
            apply(def);
        }

        public static void RegisterRoom(CustomRoomTemplate def)
        {
            Rooms.RemoveAll(x => x.Id == def.Id);
            Rooms.Add(def);
            LoaderLog.Info($"[DungeonBridge] Registered room: {def.Id} — declarative, native room-gen hook pending assembly anchor");
        }

        public static void PatchRoom(string id, Action<CustomRoomTemplate> apply)
        {
            var def = Rooms.Find(x => x.Id == id);
            if (def == null) { LoaderLog.Warn($"[DungeonBridge] Patch room target not found: {id}"); return; }
            apply(def);
        }

        public static string[] ListBiomes() => Biomes.Select(x => x.Id).ToArray();

        public static string[] ListRooms() => Rooms.Select(x => x.Id).ToArray();
    }
}
