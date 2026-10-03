# Project Context

## Environment
- Language: TypeScript (API, CLI, demo mod) and C# (BepInEx IL2CPP loader); Python metadata tools.
- Runtime: Node.js >=18, .NET 6, Unity IL2CPP game (Pancake/Vampire Crawlers).
- Build: `npm run build`; package-specific builds include `npm run build:api`, `npm run build:demo`; loader is built with `dotnet build packages/loader/PancakeModLoader.csproj` when the game/BepInEx references are available.
- Test/typecheck: `npm run typecheck` and `npm run lint` (both root TS `tsc --noEmit`); package typechecks; no test suite was found during initial scan.
- Package manager: npm (`package-lock.json`), workspaces for `packages/api`, `packages/cli`, and `mods/demo-mod`.

## Project Type
- [x] Library/tooling monorepo (TypeScript modding API + CLI + C# runtime loader + demo mod).
- [ ] Application / microservice.

## Infrastructure
- Container/orchestration: None found.
- CI/CD: Not found in initial root scan.
- Cloud: None found.
- Runtime integration: BepInEx 6 IL2CPP and Jint; `devenv.nix` provisions game/mod environment when available.

## Structure
- API source: `packages/api/src/index.ts`, `packages/api/src/registry.ts`.
- Loader: `packages/loader/*.cs`; live Jint API/global construction currently in `Shared.cs`.
- CLI: `packages/cli/src`.
- Test/example mod: `mods/demo-mod/src/index.ts`, manifest `mods/demo-mod/mod.json`.
- Game metadata tools: `tools/dump-metadata.py`, `tools/catalog-inspect.py`.
- Tests: no separate tests directory or test scripts observed.
- Docs: `README.md`, `pickup.md`.
- Entry: CLI `packages/cli/src/cli.ts`; loader `Plugin.BepInEx.cs`; demo entry is built `mods/demo-mod/dist/index.js`.

## Conventions
- TypeScript strict mode, ESM, semicolon style, camelCase properties, PascalCase exported types/interfaces.
- C# namespace `PancakeModLoader`, PascalCase methods/types, Harmony patches, dynamic/reflection-oriented integration with IL2CPP.
- API types are declared in TS and mirrored manually by JS runtime bridge in `Shared.cs`; parity must be implemented explicitly.
- `vc` is a global object injected per mod by Jint. Current runtime bridges include cards, arcanas (registration parser only), enemies, audio, console/log, and a stub hook interface; declared relic/gem/evolution/dungeon namespaces are not fully represented by Jint.
- Current custom enemy implementation is partial: definition parsing and sprite animation exist, but real spawn/config/stat wiring contains placeholder behavior. Arcana native integration is commented-out backup code, not active.
- Demo mod has a local fallback shim for local build/testing and currently demonstrates cards only.

## Initial Findings / Constraints
- Existing API already declares types for cards, gems, arcanas, relics, enemies, evolutions, biomes/rooms (dungeons), state, and hooks, but runtime implementation is significantly narrower.
- Game-specific APIs must be checked against the installed game assemblies/metadata before claiming runtime behavior. If unavailable, introduce honest declarative extension points and test the demo/API/bridge contract without asserting native game integration.
- Inverse is known by user request as the starter scenario: relic/option makes an in-run UI toggle possible, alters run mode/state, and map/content customization are desired. Wiki research is needed to determine Crawlers-specific semantics versus Vampire Survivors.
- Initial git status: only `.opencode/` is untracked; tracked source changes were not present. Existing `.opencode/todo.md` was a starter placeholder.
