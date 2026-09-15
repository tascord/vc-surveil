{
  pkgs,
  lib,
  config,
  inputs,
  ...
}:

{
  packages = with pkgs; [
    git
    jq
    nodejs_22
    typescript
    # dotnet 8 for BepInEx loader
    dotnet-sdk_8
    python3
    python3Packages.pip
    curl
    unzip
    ripgrep
  ];

  languages.javascript = {
    enable = true;
    package = pkgs.nodejs_22;
    npm.enable = true;
  };

  # dotnet tooling via packages; no devenv dotnet module needed

  env.BEPINEX_URL = "https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip";
  env.VC_SURVEIL_AUTO_SETUP = "1";
  env.DOTNET_CLI_TELEMETRY_OPTOUT = "1";

  scripts.surveil.exec = ''
    set -e
    ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    # Auto-build CLI if dist missing (no npm install required — uses nix-provided node/tsc)
    if [ ! -f "$ROOT/packages/cli/dist/cli.js" ]; then
      echo "surveil: building @vc-surveil/surveil (first run)..." >&2
      # Ensure deps present without full npm install: use local tsc from nix
      if [ ! -d "$ROOT/node_modules" ]; then
        (cd "$ROOT" && npm install --ignore-scripts 2>&1 | tail -n 5)
      fi
      (cd "$ROOT" && ${pkgs.nodejs_22}/bin/npx tsc -p packages/cli/tsconfig.json 2>&1 | tail -n 20)
      (cd "$ROOT" && ${pkgs.nodejs_22}/bin/npx tsc -p packages/api/tsconfig.json 2>&1 | tail -n 20)
    fi
    exec ${pkgs.nodejs_22}/bin/node "$ROOT/packages/cli/dist/cli.js" "$@"
  '';
  scripts.surveil.description = "vc-surveil CLI — install/launch/list mods (replaces npx surveil)";

  scripts.surveil-build.exec = ''
    set -e
    ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    if [ ! -d "$ROOT/node_modules" ]; then
      echo "installing deps..." >&2
      (cd "$ROOT" && npm install --ignore-scripts)
    fi
    exec npm --prefix "$ROOT" run build
  '';
  scripts.surveil-build.description = "Build all workspaces (@vc-surveil/api, surveil CLI, demo-mod)";

  scripts.surveil-publish.exec = ''
    set -e
    ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    exec bash "$ROOT/publish.sh" "$@"
  '';
  scripts.surveil-publish.description = "Build and publish @vc-surveil/api + @vc-surveil/surveil (wraps ./publish.sh)";

  # Auto-provision BepInEx + loader so `surveil setup` is not required
  scripts.surveil-ensure-game.exec = ''
    set -e
    GAME="$HOME/.local/share/Steam/steamapps/common/Vampire Crawlers"
    if [ ! -d "$GAME" ]; then
      echo "surveil-ensure-game: game dir not found at $GAME — skipping (install game or set VC_GAME_DIR)" >&2
      exit 0
    fi
    # Fix perms that BepInEx zip sometimes ships as 000 (owner still can chmod)
    chmod u+rx "$GAME" 2>/dev/null || true
    if [ -d "$GAME/BepInEx" ]; then chmod -R u+rwX "$GAME/BepInEx" 2>/dev/null || true; fi
    if [ -d "$GAME/dotnet" ]; then chmod -R u+rwX "$GAME/dotnet" 2>/dev/null || true; fi
    # 1. Ensure ~/.vc-mods exists and demo-mod present
    mkdir -p "$HOME/.vc-mods" 2>/dev/null || true
    if [ ! -d "$HOME/.vc-mods/demo-mod" ] && [ -d "$DEVENV_ROOT/mods/demo-mod" 2>/dev/null ]; then
      echo "surveil-ensure-game: seeding ~/.vc-mods/demo-mod from repo demo-mod..." >&2
      cp -R "$DEVENV_ROOT/mods/demo-mod" "$HOME/.vc-mods/demo-mod" 2>/dev/null || cp -R "./mods/demo-mod" "$HOME/.vc-mods/demo-mod" 2>/dev/null || true
    elif [ ! -d "$HOME/.vc-mods/demo-mod" ] && [ -d "./mods/demo-mod" ]; then
      cp -R "./mods/demo-mod" "$HOME/.vc-mods/demo-mod" 2>/dev/null || true
    fi
    # Ensure demo-mod in ~/.vc-mods is built (dist present)
    if [ -d "$HOME/.vc-mods/demo-mod" ] && [ ! -f "$HOME/.vc-mods/demo-mod/dist/index.js" ]; then
      echo "  building ~/.vc-mods/demo-mod..." >&2
      (cd "$HOME/.vc-mods/demo-mod" && npm install --ignore-scripts 2>&1 | tail -n 3; npx tsc -p tsconfig.json 2>&1 | tail -n 5) || true
    fi
    # 2. Ensure BepInEx present and compatible with Unity 6 (metadata 31)
    NEED_BEPINEX=0
    if [ ! -f "$GAME/BepInEx/core/BepInEx.Core.dll" ] || [ ! -f "$GAME/doorstop_config.ini" ]; then
      NEED_BEPINEX=1
    elif [ -f "$GAME/BepInEx/LogOutput.log" ] && grep -q "Unsupported metadata version found" "$GAME/BepInEx/LogOutput.log" 2>/dev/null; then
      echo "surveil-ensure-game: detected BepInEx metadata v31 failure — upgrading to build #788..." >&2
      NEED_BEPINEX=1
    elif [ -f "$GAME/BepInEx/core/LibCpp2IL.dll" ]; then
      LIBCPP_SIZE=$(stat -c%s "$GAME/BepInEx/core/LibCpp2IL.dll" 2>/dev/null || echo 0)
      if [ "$LIBCPP_SIZE" -lt 300000 ]; then
        echo "surveil-ensure-game: detected outdated BepInEx (< be.764) without metadata v31 support — upgrading..." >&2
        NEED_BEPINEX=1
      fi
    fi

    if [ "$NEED_BEPINEX" = "1" ]; then
      if [ -z "''${BEPINEX_URL:-}" ] || echo "''${BEPINEX_URL:-}" | grep -q "v6.0.0-pre"; then
        BEPINEX_URL="https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip"
      fi
      TMP="$(mktemp /tmp/bepinex-XXXX.zip)"
      echo "surveil-ensure-game: fetching BepInEx 6 BE (#788) from $BEPINEX_URL ..." >&2
      if curl -L --fail -o "$TMP" "$BEPINEX_URL" 2>&1 | tail -n 5; then
        rm -rf "$GAME/BepInEx/interop" 2>/dev/null || true
        chmod -R u+rwX "$GAME/BepInEx" "$GAME/dotnet" 2>/dev/null || true
        echo "  unzipping to $GAME ..." >&2
        unzip -o -q "$TMP" -d "$GAME" || echo "  unzip failed — install manually: surveil setup --yes" >&2
        rm -f "$TMP"
        chmod -R u+rwX "$GAME/BepInEx" "$GAME/dotnet" 2>/dev/null || true
        chmod u+rw "$GAME/doorstop_config.ini" "$GAME/winhttp.dll" 2>/dev/null || true
        echo "  BepInEx 6 BE (#788) installed ✓ (Unity 6 / metadata v31 ready)" >&2
      else
        echo "  download failed (offline?) — run: surveil setup --yes or unzip manually" >&2
        rm -f "$TMP"
      fi
    else
      echo "surveil-ensure-game: BepInEx present ✓ (Unity 6 / metadata v31 ready)" >&2
    fi
    # 3. Build + install loader if needed
    ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    # Allow DEVENV_ROOT fallback
    if [ -z "$ROOT" ] || [ ! -f "$ROOT/packages/loader/PancakeModLoader.csproj" ]; then ROOT="$DEVENV_ROOT"; fi
    if [ -z "$ROOT" ] || [ ! -f "$ROOT/packages/loader/PancakeModLoader.csproj" ]; then ROOT="."; fi
    LOADER_SRC="$ROOT/packages/loader/PancakeModLoader.csproj"
    LOADER_DLL="$ROOT/packages/loader/bin/Release/net6.0/PancakeModLoader.dll"
    DEST_DIR="$GAME/BepInEx/plugins"
    DEST_DLL="$DEST_DIR/PancakeModLoader.dll"
    if [ -f "$LOADER_SRC" ]; then
      NEED_BUILD=0
      if [ ! -f "$LOADER_DLL" ]; then NEED_BUILD=1
      elif [ "$LOADER_SRC" -nt "$LOADER_DLL" ] || [ "$ROOT/packages/loader/DevConsole.cs" -nt "$LOADER_DLL" ] || [ "$ROOT/packages/loader/Plugin.cs" -nt "$LOADER_DLL" ]; then NEED_BUILD=1; fi
      if [ "$NEED_BUILD" = 1 ]; then
        echo "surveil-ensure-game: building PancakeModLoader.dll ..." >&2
        (cd "$ROOT" && dotnet build packages/loader/PancakeModLoader.csproj -c Release 2>&1 | tail -n 20)
      fi
      if [ -f "$LOADER_DLL" ]; then
        mkdir -p "$DEST_DIR" 2>/dev/null || true
        if [ ! -f "$DEST_DLL" ] || [ "$LOADER_DLL" -nt "$DEST_DLL" ]; then
          cp "$LOADER_DLL" "$DEST_DLL" 2>/dev/null || true
          # Ensure game BepInEx perms stay writable
          chmod u+rw "$DEST_DLL" 2>/dev/null || true
          echo "  loader installed → $DEST_DLL ✓" >&2
        else
          echo "  loader up to date ✓" >&2
        fi
      fi
    fi
    # 4. Sync mods for Proton (symlink each ~/.vc-mods/* into game/Mods for Wine visibility)
    mkdir -p "$GAME/Mods" 2>/dev/null || true
    # Ensure BepInEx perms remain writable for future launches
    chmod -R u+rwX "$GAME/BepInEx" 2>/dev/null || true
    chmod u+rw "$GAME/doorstop_config.ini" "$GAME/winhttp.dll" 2>/dev/null || true
    for d in "$HOME/.vc-mods"/*; do
      [ -d "$d" ] || continue
      base="$(basename "$d")"
      dest="$GAME/Mods/$base"
      if [ ! -e "$dest" ]; then
        ln -s "$d" "$dest" 2>/dev/null || cp -R "$d" "$dest" 2>/dev/null || true
        echo "  linked $base → game/Mods/" >&2
      fi
    done
  '';
  scripts.surveil-ensure-game.description = "Ensure BepInEx + loader + ~/.vc-mods/demo-mod are present (auto-run on shell)";

  enterShell = ''
    echo "— vc-surveil devenv —"
    echo "node $(node --version)  npm $(npm --version)  tsc $(tsc --version)"
    dotnet --version 2>/dev/null | sed 's/^/dotnet /' || echo "dotnet not found"
    python3 --version 2>/dev/null | sed 's/^/python /'
    echo ""
    if [ "''${VC_SURVEIL_AUTO_SETUP:-1}" = "1" ]; then
      echo "→ ensuring run deps (BepInEx + loader + ~/.vc-mods) — no surveil setup needed..." >&2
      surveil-ensure-game 2>&1 | sed 's/^/  /'
      echo ""
    fi
    if command -v surveil >/dev/null 2>&1; then
      echo "surveil available as devenv script — no npm install needed:"
      surveil --help 2>&1 | head -n 25
    fi
    echo ""
    echo "pickup.md next steps:"
    head -n 20 pickup.md | tail -n 15
    echo ""
    echo "try: surveil --help"
    echo "     surveil install https://github.com/<org>/<mod>  (replaces npx surveil install)"
    echo "     surveil launch --dry-run   (or npx @vc-surveil/surveil launch)"
    echo "     surveil list               (mods in ~/.vc-mods)"
    echo "     surveil-build  (or npm run build)"
    echo "     surveil-publish --dry-run  (or ./publish.sh --dry-run)"
    echo "     Focus game → press \` (backtick) for dev console — try: help, cards, give demo-mod:test_strike"
    echo "     Card rarity: see CardDropConfig (pools/weight/luckFactor) — stats in Reward/CardRewardPool + ChestTier"
  '';

  # no services needed yet
}
