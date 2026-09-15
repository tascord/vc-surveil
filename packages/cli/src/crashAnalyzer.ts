import fs from "node:fs";
import path from "node:path";
import os from "node:os";

export interface CrashFrame {
  cleaned: string;
  category: "mod" | "game" | "unity" | "system";
  isFaultOrigin?: boolean;
}

export interface CrashReport {
  hasCrash: boolean;
  exceptionType: string;
  message: string;
  exitCode?: number;
  origin?: {
    category: "mod" | "game" | "unity" | "system";
    method: string;
    fileHint?: string;
  };
  frames: CrashFrame[];
  sourceFile?: string;
  diagnosis?: string;
}

const APP_ID = "3265700";

/**
 * Scans /proc to find any running Vampire Crawlers process IDs.
 */
export function findGamePids(): string[] {
  const pids: string[] = [];
  try {
    const entries = fs.readdirSync("/proc");
    for (const entry of entries) {
      if (!/^\d+$/.test(entry) || entry === String(process.pid)) continue;
      try {
        const cmdline = fs.readFileSync(`/proc/${entry}/cmdline`, "utf8");
        if (
          (cmdline.includes("Vampire Crawlers.exe") || cmdline.includes("Vampire Crawlers")) &&
          !cmdline.includes("surveil") &&
          !cmdline.includes("node")
        ) {
          pids.push(entry);
        }
      } catch {}
    }
  } catch {}
  return pids;
}

export function isGameRunning(): boolean {
  return findGamePids().length > 0;
}

/**
 * Checks Steam's gameprocess_log.txt for the last known exit code of Vampire Crawlers.
 */
export function getSteamLastExitCode(): number | undefined {
  const logPath = path.join(os.homedir(), ".local/share/Steam/logs/gameprocess_log.txt");
  if (!fs.existsSync(logPath)) return undefined;
  try {
    const text = fs.readFileSync(logPath, "utf8");
    const lines = text.split(/\r?\n/).slice(-80);
    for (let i = lines.length - 1; i >= 0; i--) {
      const m = lines[i].match(/AppID\s+3265700\s+no longer tracking PID\s+\d+,\s*exit code\s+(-?\d+)/);
      if (m) {
        return parseInt(m[1], 10);
      }
    }
  } catch {}
  return undefined;
}

/**
 * Cleans an IL2CPP stack frame line.
 * Removes repetitive memory offsets, fixes generic notation, trims whitespace.
 */
export function cleanStackFrame(rawLine: string): { cleaned: string; category: "mod" | "game" | "unity" | "system" } {
  let cleaned = rawLine.trim().replace(/^(?:at|in)\s+/, "");
  // Remove IL2CPP memory offsets like: <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
  cleaned = cleaned.replace(/\s*<0x[0-9a-fA-F]+\s*\+\s*0x[0-9a-fA-F]+>(\s*\d+)?\s*in\s*<[0-9a-fA-F]+>:\d*/g, "");
  // Fix generic arity: UniTaskCompletionSourceCore`1[TResult] -> UniTaskCompletionSourceCore<TResult>
  cleaned = cleaned.replace(/`\d+\[(.*?)\]/g, "<$1>");
  // Remove redundant whitespace before parameter list
  cleaned = cleaned.replace(/\s+\(/g, "(");

  let category: "mod" | "game" | "unity" | "system" = "system";
  if (
    cleaned.startsWith("PancakeModLoader.") ||
    cleaned.startsWith("JsRuntime") ||
    cleaned.startsWith("CardBridge") ||
    cleaned.startsWith("DevConsole") ||
    cleaned.startsWith("demo-mod") ||
    cleaned.includes("PancakeModLoader")
  ) {
    category = "mod";
  } else if (cleaned.startsWith("Nosebleed.") || cleaned.startsWith("GameLogic.")) {
    category = "game";
  } else if (cleaned.startsWith("UnityEngine.") || cleaned.startsWith("Unity.")) {
    category = "unity";
  }

  return { cleaned, category };
}

/**
 * Parses exception details and stack traces from raw log text.
 */
export function parseStackTrace(text: string): {
  exceptionType: string;
  message: string;
  frames: CrashFrame[];
} {
  const lines = text.split(/\r?\n/);
  const frames: CrashFrame[] = [];
  let exceptionType = "";
  let message = "";

  for (let i = 0; i < lines.length; i++) {
    const l = lines[i].trim();
    if (!l) continue;

    // Detect fatal errors / exceptions
    if (!exceptionType) {
      const fatalMatch = l.match(/(?:Fatal error\.\s*|Unhandled Exception:\s*|\[CRASH\]\s*(?:Unhandled Exception.*?:\s*)?)([A-Za-z0-9_.]+(?:Exception|Error))(?::\s*(.*))?/i);
      if (fatalMatch) {
        exceptionType = fatalMatch[1];
        message = (fatalMatch[2] || "").trim();
        continue;
      }
      const genericExMatch = l.match(/^([A-Za-z0-9_.]+(?:Exception|Error)):\s*(.*)/i);
      if (genericExMatch && !genericExMatch[1].startsWith("System.Diagnostics")) {
        exceptionType = genericExMatch[1];
        message = genericExMatch[2].trim();
        continue;
      }
      if (l.includes("Native Crash Reporting") || l.includes("Crash!!!") || l.includes("Segmentation fault")) {
        exceptionType = "NativeCrash (SIGSEGV)";
        message = l;
        continue;
      }
    }

    // Detect stack frame lines
    if (l.startsWith("at ") || l.startsWith("in ") || /^[A-Za-z0-9_.]+::/.test(l) || l.includes(" <0x00000 + 0xffffffff>")) {
      const { cleaned, category } = cleanStackFrame(l);
      if (cleaned) {
        frames.push({ cleaned, category });
      }
    }
  }

  // Mark the primary fault origin
  const modOrigin = frames.find((f) => f.category === "mod");
  if (modOrigin) {
    modOrigin.isFaultOrigin = true;
  } else if (frames.length > 0) {
    frames[0].isFaultOrigin = true;
  }

  return { exceptionType, message, frames };
}

/**
 * Collapses repetitive system/engine frames to keep the trace readable.
 */
export function collapseFrames(frames: CrashFrame[]): { displayLines: string[]; collapsedCount: number } {
  const result: string[] = [];
  let consecutiveSystem = 0;
  let collapsedCount = 0;

  const flushSystem = () => {
    if (consecutiveSystem > 2) {
      result.push(`    \x1b[90m┆ ... [${consecutiveSystem} internal engine / async task frames collapsed] ...\x1b[0m`);
      collapsedCount += consecutiveSystem;
    } else if (consecutiveSystem > 0) {
      // Print them if only 1 or 2
      for (let j = 0; j < consecutiveSystem; j++) {
        const frame = systemBuffer[j];
        result.push(formatFrameLine(frame));
      }
    }
    consecutiveSystem = 0;
    systemBuffer.length = 0;
  };

  const systemBuffer: CrashFrame[] = [];

  function formatFrameLine(frame: CrashFrame): string {
    const prefix = frame.isFaultOrigin ? "\x1b[31m  →\x1b[0m " : "    ";
    let tag = "";
    let color = "\x1b[0m";

    if (frame.category === "mod") {
      tag = "\x1b[35m[MOD]\x1b[0m  ";
      color = "\x1b[1;37m"; // Bold white
    } else if (frame.category === "game") {
      tag = "\x1b[33m[GAME]\x1b[0m ";
      color = "\x1b[37m";
    } else if (frame.category === "unity") {
      tag = "\x1b[34m[UNITY]\x1b[0m";
      color = "\x1b[90m";
    } else {
      tag = "\x1b[90m[SYS]\x1b[0m  ";
      color = "\x1b[90m";
    }

    return `${prefix}${tag} ${color}${frame.cleaned}\x1b[0m`;
  }

  for (const f of frames) {
    if (f.category === "system" && !f.isFaultOrigin) {
      consecutiveSystem++;
      systemBuffer.push(f);
    } else {
      flushSystem();
      result.push(formatFrameLine(f));
    }
  }
  flushSystem();

  return { displayLines: result, collapsedCount };
}

/**
 * Scans log files and returns a structured CrashReport if an issue occurred.
 */
export function analyzeCrash(gameDir: string): CrashReport | null {
  const home = os.homedir();
  const exitCode = getSteamLastExitCode();

  // 1. Check direct crash log if written by PancakeModLoader
  const lastCrashLog = path.join(home, ".vc-mods/last-crash.log");
  if (fs.existsSync(lastCrashLog)) {
    try {
      const content = fs.readFileSync(lastCrashLog, "utf8");
      if (content.trim()) {
        const parsed = parseStackTrace(content);
        if (parsed.exceptionType || parsed.frames.length) {
          // Remove processed crash log so it doesn't trigger on subsequent runs
          try { fs.unlinkSync(lastCrashLog); } catch {}
          return buildReport(parsed.exceptionType, parsed.message, parsed.frames, lastCrashLog, exitCode);
        }
      }
    } catch {}
  }

  // 2. Candidate log files to inspect
  const candidateLogs = [
    path.join(home, ".local/share/Steam/steamapps/compatdata", APP_ID, "pfx/drive_c/users/steamuser/AppData/LocalLow/Nosebleed Interactive/Vampire Crawlers/Player.log"),
    path.join(home, ".local/share/Steam/steamapps/compatdata", APP_ID, "pfx/drive_c/users/steamuser/AppData/LocalLow/Nosebleed Interactive/Vampire Crawlers/Player-prev.log"),
    path.join(gameDir, "BepInEx/LogOutput.log"),
    path.join(gameDir, "Player.log"),
  ];

  for (const logPath of candidateLogs) {
    if (!fs.existsSync(logPath)) continue;
    try {
      const txt = fs.readFileSync(logPath, "utf8");
      // Read the last 200KB of the file
      const tailTxt = txt.length > 200000 ? txt.slice(-200000) : txt;

      if (
        tailTxt.includes("Fatal error.") ||
        tailTxt.includes("Unhandled Exception:") ||
        tailTxt.includes("AccessViolationException") ||
        tailTxt.includes("NullReferenceException") ||
        tailTxt.includes("MissingMethodException") ||
        tailTxt.includes("[CRASH]") ||
        tailTxt.includes("Crash!!!") ||
        tailTxt.includes("Native Crash Reporting")
      ) {
        const parsed = parseStackTrace(tailTxt);
        if (parsed.exceptionType || parsed.frames.length) {
          return buildReport(parsed.exceptionType, parsed.message, parsed.frames, logPath, exitCode);
        }
      }
    } catch {}
  }

  // If exit code was abnormal (-1 or > 0) but no stack trace found in logs
  if (exitCode !== undefined && exitCode !== 0) {
    return {
      hasCrash: true,
      exceptionType: exitCode === -1 ? "ProcessAborted (SIGKILL/SIGABRT)" : `ExitCode (${exitCode})`,
      message: `The game process terminated unexpectedly with exit code ${exitCode}.`,
      exitCode,
      frames: [],
      diagnosis: "The game process closed abnormally. Check Steam logs or run with --tail for full diagnostics.",
    };
  }

  return null;
}

function buildReport(
  exceptionType: string,
  message: string,
  frames: CrashFrame[],
  sourceLog: string,
  exitCode?: number
): CrashReport {
  const originFrame = frames.find((f) => f.isFaultOrigin) || frames[0];
  let fileHint: string | undefined;

  if (originFrame?.cleaned.includes("SpriteLoader")) {
    fileHint = "packages/loader/SpriteLoader.cs";
  } else if (originFrame?.cleaned.includes("CardBridge")) {
    fileHint = "packages/loader/CardBridge.cs";
  } else if (originFrame?.cleaned.includes("CardPatches")) {
    fileHint = "packages/loader/CardPatches.cs";
  } else if (originFrame?.cleaned.includes("DevConsole")) {
    fileHint = "packages/loader/DevConsole.cs";
  } else if (originFrame?.cleaned.includes("JsRuntime")) {
    fileHint = "packages/loader/JsRuntime.cs";
  }

  let diagnosis: string | undefined;
  if (exceptionType.includes("AccessViolationException")) {
    diagnosis = "Illegal unmanaged memory access during native IL2CPP call. Bypassed managed struct marshalling is required.";
  } else if (exceptionType.includes("MissingMethodException")) {
    diagnosis = "Missing method signature in IL2CPP runtime. Method was stripped or relocated in the game binary.";
  } else if (exceptionType.includes("NullReferenceException")) {
    diagnosis = "Attempted to access a null object instance or uninitialized config property.";
  } else if (originFrame?.category === "mod") {
    diagnosis = `Fault originated in mod loader code (${fileHint || originFrame.cleaned}).`;
  }

  return {
    hasCrash: true,
    exceptionType: exceptionType || "FatalError",
    message: message || "Application encountered an unhandled exception.",
    exitCode,
    origin: originFrame
      ? {
          category: originFrame.category,
          method: originFrame.cleaned,
          fileHint,
        }
      : undefined,
    frames,
    sourceFile: sourceLog,
    diagnosis,
  };
}

/**
 * Renders an ANSI formatted box for the crash report.
 */
export function renderCrashReport(report: CrashReport): string {
  const width = 78;
  const hLine = "─".repeat(width - 2);
  const out: string[] = [];

  out.push(`\n\x1b[1;31m╭${hLine}╮\x1b[0m`);
  out.push(`\x1b[1;31m│\x1b[0m  \x1b[1;31m!! GAME CRASH DETECTED\x1b[0m${" ".repeat(width - 25)}\x1b[1;31m│\x1b[0m`);
  out.push(`\x1b[1;31m├${hLine}┤\x1b[0m`);

  const printRow = (label: string, value: string, valColor: string = "\x1b[0m") => {
    const rawLen = label.length + value.length + 3;
    const padding = Math.max(0, width - 4 - rawLen);
    out.push(`\x1b[1;31m│\x1b[0m  \x1b[1m${label}\x1b[0m: ${valColor}${value}\x1b[0m${" ".repeat(padding)} \x1b[1;31m│\x1b[0m`);
  };

  printRow("Exception", report.exceptionType, "\x1b[1;33m");
  if (report.message) {
    printRow("Message  ", report.message.length > 58 ? report.message.slice(0, 55) + "..." : report.message);
  }
  if (report.exitCode !== undefined) {
    printRow("Exit Code", String(report.exitCode), report.exitCode === 0 ? "\x1b[32m" : "\x1b[31m");
  }
  if (report.origin) {
    const originTag = report.origin.category === "mod" ? "[MOD] " : "";
    printRow("Origin   ", `${originTag}${report.origin.method.split("(")[0]}`, "\x1b[1;35m");
    if (report.origin.fileHint) {
      printRow("File     ", report.origin.fileHint, "\x1b[36m");
    }
  }
  if (report.sourceFile) {
    printRow("Log File ", path.basename(report.sourceFile), "\x1b[90m");
  }

  if (report.frames.length > 0) {
    out.push(`\x1b[1;31m├${hLine}┤\x1b[0m`);
    out.push(`\x1b[1;31m│\x1b[0m  \x1b[1;37mReadable Call Stack:\x1b[0m${" ".repeat(width - 24)}\x1b[1;31m│\x1b[0m`);
    const { displayLines } = collapseFrames(report.frames);
    for (const dl of displayLines.slice(0, 16)) {
      out.push(`\x1b[1;31m│\x1b[0m  ${dl}`);
    }
    if (displayLines.length > 16) {
      out.push(`\x1b[1;31m│\x1b[0m     \x1b[90m... [${displayLines.length - 16} more frames truncated]\x1b[0m`);
    }
  }

  if (report.diagnosis) {
    out.push(`\x1b[1;31m├${hLine}┤\x1b[0m`);
    out.push(`\x1b[1;31m│\x1b[0m  \x1b[1;36mDiagnosis:\x1b[0m${" ".repeat(width - 16)}\x1b[1;31m│\x1b[0m`);
    out.push(`\x1b[1;31m│\x1b[0m  \x1b[37m${report.diagnosis}\x1b[0m`);
  }

  out.push(`\x1b[1;31m╰${hLine}╯\x1b[0m\n`);
  return out.join("\n");
}
