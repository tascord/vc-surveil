using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace PancakeModLoader
{
    /// Single wall/overlay tile entry. Mirrors `MapTile` in packages/api/src/index.ts.
    public class MapTile
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public string Tile { get; set; } = "";
        public bool Walkable { get; set; } = true;
    }

    /// Wall overlay for a room map. Mirrors `WallDef` in packages/api/src/index.ts.
    public class WallDef
    {
        public List<MapTile> Tiles { get; set; } = new();
        public string Material { get; set; } = "";
    }

    /// Prop / destructible placement within a room map. Mirrors `PropPlacement`.
    public class PropPlacement
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public bool Destructible { get; set; } = false;
        public int Hp { get; set; } = 0;
        public List<string> Drops { get; set; } = new();
        public string Interact { get; set; } = "";
    }

    /// Light source within a room map. Mirrors `LightSourceDef`.
    public class LightSourceDef
    {
        public string Id { get; set; } = "";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public float Radius { get; set; } = 0;
        public string Color { get; set; } = "";
        public bool Flicker { get; set; } = false;
    }

    /// Exit / doorway linking to another room. Mirrors `MapExit`.
    public class MapExit
    {
        public string Id { get; set; } = "";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public string ToRoom { get; set; } = "";
        public string Direction { get; set; } = "";
    }

    /// Single entry in a room map's encounter table. Mirrors `{ enemyId, weight }`.
    public class MapEncounterEntry
    {
        public string EnemyId { get; set; } = "";
        public float Weight { get; set; } = 0;
    }

    /// Single room map: fixed-size tile grid plus props, lights, and exits.
    /// `Tiles` holds `Height` string rows of tile keys, each `Width` keys long
    /// (keys resolved via `MapPackageDef.TileMaterials`). Mirrors `RoomMapDef`.
    public class RoomMapDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;
        public List<string> Tiles { get; set; } = new();
        public WallDef Walls { get; set; }
        public List<PropPlacement> Props { get; set; } = new();
        public List<LightSourceDef> Lights { get; set; } = new();
        public List<MapExit> Exits { get; set; } = new();
        public List<MapEncounterEntry> EncounterTable { get; set; } = new();
    }

    /// Map package: one biome's worth of room maps sharing a tile-material set.
    /// `BiomeId` is a biome id (vanilla texture sets: `madforest`,
    /// `inlaidlibrary`, `dairyplant`, `gallotower`, `capellamagna`, `moongolow`,
    /// `tinybridge`) and `TileMaterials` maps tile keys to material/texture names.
    /// Mirrors `MapPackageDef` in packages/api/src/index.ts.
    public class MapPackageDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string BiomeId { get; set; } = "";
        public List<RoomMapDef> Rooms { get; set; } = new();
        public Dictionary<string, string> TileMaterials { get; set; } = new();
        public string ModDirectory { get; set; } = "";
    }

    /// Declarative tile-grid map store.
    /// Native room-template injection pending assembly anchor — defs are kept for
    /// `vc.maps` round-trips; no native roomTemplateDatabase /
    /// mapTileDefinitionDatabase / propDefinitionDatabase injection is wired yet.
    public static class MapBridge
    {
        public static Dictionary<string, MapPackageDef> Packages { get; } =
            new Dictionary<string, MapPackageDef>(StringComparer.OrdinalIgnoreCase);

        public static void RegisterMap(MapPackageDef pkg)
        {
            if (pkg == null || string.IsNullOrEmpty(pkg.Id))
            {
                LoaderLog.Warn("[MapBridge] RegisterMap called with null/empty id; ignoring.");
                return;
            }
            Packages[pkg.Id] = pkg;
            LoaderLog.Info($"[MapBridge] Registered map package: {pkg.Id} (biome '{pkg.BiomeId}', {pkg.Rooms?.Count ?? 0} rooms) — declarative, native room-template injection pending assembly anchor");
        }

        public static void PatchMap(string id, Action<MapPackageDef> apply)
        {
            if (string.IsNullOrEmpty(id) || apply == null) return;
            if (!Packages.TryGetValue(id, out var pkg))
            {
                LoaderLog.Warn($"[MapBridge] Patch map target not found: {id}");
                return;
            }
            apply(pkg);
        }

        public static MapPackageDef FindMap(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Packages.TryGetValue(id, out var pkg);
            return pkg;
        }

        public static string[] ListMaps() => Packages.Keys.ToArray();

        /// Parse a `map.json` payload into a package and register it.
        /// Pure System.Text.Json, no Unity refs — safe in CI / menu contexts.
        public static MapPackageDef LoadPackageFromJson(string json, string modDir)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                LoaderLog.Warn("[MapBridge] LoadPackageFromJson called with empty json; ignoring.");
                return null;
            }
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pkg = JsonSerializer.Deserialize<MapPackageDef>(json, options);
                if (pkg == null || string.IsNullOrEmpty(pkg.Id))
                {
                    LoaderLog.Warn("[MapBridge] LoadPackageFromJson: missing package id; ignoring.");
                    return null;
                }
                pkg.ModDirectory = modDir ?? "";
                if (pkg.Rooms == null) pkg.Rooms = new List<RoomMapDef>();
                if (pkg.TileMaterials == null) pkg.TileMaterials = new Dictionary<string, string>();
                foreach (var room in pkg.Rooms)
                {
                    if (room == null) continue;
                    if (room.Tiles == null) room.Tiles = new List<string>();
                    if (room.Walls != null && room.Walls.Tiles == null) room.Walls.Tiles = new List<MapTile>();
                    if (room.Props == null) room.Props = new List<PropPlacement>();
                    if (room.Lights == null) room.Lights = new List<LightSourceDef>();
                    if (room.Exits == null) room.Exits = new List<MapExit>();
                    if (room.EncounterTable == null) room.EncounterTable = new List<MapEncounterEntry>();
                    foreach (var prop in room.Props)
                    {
                        if (prop != null && prop.Drops == null) prop.Drops = new List<string>();
                    }
                }
                RegisterMap(pkg);
                return pkg;
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[MapBridge] LoadPackageFromJson failed: {ex.Message}");
                return null;
            }
        }

        /// Load a zipped map package: reads the `map.json` entry and registers it.
        /// Asset files (tile materials, prop sprites) are passthrough paths resolved
        /// against `modDir` at runtime — nothing is extracted here.
        public static MapPackageDef LoadPackageFromZip(string zipPath, string modDir)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                LoaderLog.Warn($"[MapBridge] LoadPackageFromZip: file not found: {zipPath}");
                return null;
            }
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    ZipArchiveEntry entry = null;
                    foreach (var e in archive.Entries)
                    {
                        if (string.Equals(e.Name, "map.json", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.FullName, "map.json", StringComparison.OrdinalIgnoreCase))
                        {
                            entry = e;
                            break;
                        }
                    }
                    if (entry == null)
                    {
                        LoaderLog.Warn($"[MapBridge] LoadPackageFromZip: no map.json entry in {zipPath}");
                        return null;
                    }
                    string json;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        json = reader.ReadToEnd();
                    }
                    var effectiveModDir = modDir ?? Path.GetDirectoryName(zipPath) ?? "";
                    var pkg = LoadPackageFromJson(json, effectiveModDir);
                    if (pkg != null)
                        LoaderLog.Info($"[MapBridge] Loaded map package '{pkg.Id}' from zip {zipPath} (map.json + assets passthrough via ModDirectory='{effectiveModDir}')");
                    return pkg;
                }
            }
            catch (Exception ex)
            {
                LoaderLog.Error($"[MapBridge] LoadPackageFromZip failed for {zipPath}: {ex.Message}");
                return null;
            }
        }
    }
}
