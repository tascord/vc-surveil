import path from "node:path";

/**
 * Filters and formats raw game and mod loader logs into clean, readable terminal output.
 */

// Patterns of noisy engine spam to suppress
const SUPPRESS_PATTERNS: RegExp[] = [
  /^Unloading \d+ (?:Unused|unused)/i,
  /^Total: \d+\.\d+ ms \(FindLiveObjects:/i,
  /^Fallback handler could not load library/i,
  /^Setting breakpad minidump AppID/i,
  /^Steam_SetMinidumpSteamID/i,
  /^GfxDevice: creating device/i,
  /^Initialize engine version:/i,
  /^CrashReporting: initialized/i,
  /^CrashReport: Crash report submitted/i,
  /^Mono: successfully initialized/i,
  /^PlayerMain: player initialized/i,
  /^D3D11: /i,
  /^Vulkan: /i,
  /^WARNING: Shader Unsupported/i,
];

export function shouldSuppress(line: string): boolean {
  for (const p of SUPPRESS_PATTERNS) {
    if (p.test(line)) return true;
  }
  return false;
}

export function formatLogLine(
  rawLine: string,
  fileName?: string,
  includeTimestamp = true
): string | null {
  const line = rawLine.trim();
  if (!line) return null;

  if (shouldSuppress(line)) return null;

  // Check for BepInEx log format: [Level   : Source] Message
  const bepMatch = line.match(/^\[([A-Za-z]+)\s*:\s*([^\]]+)\]\s*(.*)/);
  let level = "INFO";
  let source = "";
  let msg = line;

  if (bepMatch) {
    level = bepMatch[1].toUpperCase();
    source = bepMatch[2].trim();
    msg = bepMatch[3].trim();
  }

  // Strip redundant nested headers
  msg = msg.replace(/^\[PancakeModLoader\]\s*/, "");
  msg = msg.replace(/^\[Console\]\s*/, "");

  // If this line is an inline stack frame, clean it up
  if (/^\s*at\s+/.test(msg)) {
    msg = msg.replace(/\s*<0x[0-9a-fA-F]+\s*\+\s*0x[0-9a-fA-F]+>(\s*\d+)?\s*in\s*<[0-9a-fA-F]+>:\d*/g, "");
    msg = msg.replace(/`\d+\[(.*?)\]/g, "<$1>");
    return `        \x1b[90m┆ at ${msg.trim().replace(/^at\s+/, "")}\x1b[0m`;
  }

  // Determine badge and category (all badges 5 chars wide for perfect alignment)
  let badge = "\x1b[90m SYS \x1b[0m";

  if (level === "WARNING" || level === "WARN") {
    badge = "\x1b[1;33mWARN \x1b[0m";
  } else if (level === "ERROR" || level === "FATAL" || msg.startsWith("[CRASH]")) {
    badge = "\x1b[1;31m ERR \x1b[0m";
  } else if (msg.includes("[SpriteLoader]") || msg.includes("[CardVisualsHelper]")) {
    badge = "\x1b[1;35m ART \x1b[0m";
    msg = msg.replace(/^\[SpriteLoader\]\s*/, "").replace(/^\[CardVisualsHelper\]\s*/, "");
  } else if (
    msg.includes("[CardBridge]") ||
    msg.includes("[CardPatches]") ||
    msg.startsWith("Created CardConfig") ||
    msg.startsWith("Assigned custom sprite") ||
    msg.startsWith("OnPlayCard: Executing") ||
    msg.includes("ChooseCardModal") ||
    msg.includes("CardChoiceView") ||
    msg.includes("ChoosableCard") ||
    msg.includes("CardPileView")
  ) {
    badge = "\x1b[1;36mCARD \x1b[0m";
    msg = msg.replace(/^\[CardBridge\]\s*/, "").replace(/^\[CardPatches\]\s*/, "");
  } else if (msg.includes("[JsRuntime]") || msg.startsWith("Loaded mod ")) {
    badge = "\x1b[1;34m MOD \x1b[0m";
    msg = msg.replace(/^\[JsRuntime\]\s*/, "");
  } else if (msg.includes("[DevConsole]") || source.includes("DevConsole")) {
    badge = "\x1b[36m DEV \x1b[0m";
    msg = msg.replace(/^\[DevConsole\]\s*/, "");
  } else if (
    msg.startsWith("VC Surveil:") ||
    msg.startsWith("Chainloader startup complete") ||
    msg.includes("ready!")
  ) {
    badge = "\x1b[1;32mREADY\x1b[0m";
  } else if (msg.startsWith("[demo-mod]") || (source === "Console" && msg.startsWith("["))) {
    badge = "\x1b[33m JS  \x1b[0m";
  } else if (fileName && fileName.includes("Player")) {
    badge = "\x1b[90mGAME \x1b[0m";
  }

  // Simplify long Wine/Proton paths into clean relative paths
  msg = msg.replace(/S:[\\/]steamapps[\\/]common[\\/]Vampire Crawlers[\\/]/gi, "");
  msg = msg.replace(/C:[\\/]users[\\/]steamuser[\\/]\.vc-mods/gi, "~/.vc-mods");
  msg = msg.replace(/S:[\\/]/gi, "");
  msg = msg.replace(/\\/g, "/");

  // Format timestamp (HH:MM:SS)
  let tsPrefix = "";
  if (includeTimestamp) {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    const timeStr = `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
    tsPrefix = `\x1b[90m${timeStr}\x1b[0m `;
  }

  return `${tsPrefix}${badge}  ${msg}`;
}

/**
 * Line buffer helper that handles partial read chunks from streamed files.
 */
export class LogLineBuffer {
  private buffer = "";

  public processChunk(chunk: string, onLine: (line: string) => void): void {
    this.buffer += chunk;
    const lines = this.buffer.split("\n");
    // Last element is either empty (if ended with \n) or an incomplete line
    this.buffer = lines.pop() ?? "";
    for (const l of lines) {
      onLine(l);
    }
  }

  public flush(onLine: (line: string) => void): void {
    if (this.buffer.trim()) {
      onLine(this.buffer);
      this.buffer = "";
    }
  }
}
