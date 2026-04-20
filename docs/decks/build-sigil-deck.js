// Sigil Architecture Deck generator
const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.333 x 7.5
pres.title = "Sigil — Architecture Blueprint";
pres.author = "Sigil";

// ---- Palette (Midnight Executive, tuned) ----
const C = {
  navy:   "1E2761",
  deep:   "0F1530",
  mid:    "3B4BA3",
  accent: "CADCFC",
  amber:  "F9B87A",
  coral:  "F96167",
  ink:    "101326",
  body:   "2A2F4A",
  muted:  "6B7394",
  paper:  "F5F7FF",
  card:   "FFFFFF",
  rule:   "D8DEF2",
};

const F = { head: "Georgia", body: "Calibri" };

const W = 13.333, H = 7.5;

// ---- Helpers ----
function bg(slide, color) {
  slide.background = { color };
}

function motif(slide, onDark = false) {
  // Left color bar motif
  slide.addShape("rect", {
    x: 0, y: 0, w: 0.18, h: H,
    fill: { color: onDark ? C.amber : C.navy }, line: { type: "none" },
  });
  // Footer mark
  slide.addText("SIGIL · A MARK OF POWER AND BINDING", {
    x: 0.5, y: H - 0.38, w: 8, h: 0.3,
    fontFace: F.body, fontSize: 9, color: onDark ? C.accent : C.muted,
    charSpacing: 4,
  });
}

function pageNum(slide, n, total, onDark = false) {
  slide.addText(`${String(n).padStart(2, "0")} / ${String(total).padStart(2, "0")}`, {
    x: W - 1.5, y: H - 0.38, w: 1, h: 0.3,
    fontFace: F.body, fontSize: 9, color: onDark ? C.accent : C.muted,
    align: "right",
  });
}

function title(slide, text, sub) {
  slide.addText(text, {
    x: 0.5, y: 0.55, w: W - 1, h: 0.9,
    fontFace: F.head, fontSize: 34, bold: true, color: C.navy,
  });
  if (sub) {
    slide.addText(sub, {
      x: 0.5, y: 1.45, w: W - 1, h: 0.4,
      fontFace: F.body, fontSize: 14, italic: true, color: C.mid,
    });
  }
  // Tag square
  slide.addShape("rect", { x: 0.5, y: 0.42, w: 0.25, h: 0.08, fill: { color: C.amber }, line: { type: "none" } });
}

function card(slide, x, y, w, h, opts = {}) {
  slide.addShape("roundRect", {
    x, y, w, h,
    fill: { color: opts.fill || C.card },
    line: { color: opts.stroke || C.rule, width: 1 },
    rectRadius: 0.08,
  });
}

function iconDot(slide, x, y, color = C.navy) {
  slide.addShape("ellipse", { x, y, w: 0.32, h: 0.32, fill: { color }, line: { type: "none" } });
}

// ---- Slides ----
const TOTAL = 22;
let n = 0;
const next = () => ++n;

// 1. Title
{
  const s = pres.addSlide();
  bg(s, C.deep);
  // Accent band
  s.addShape("rect", { x: 0, y: 0, w: 0.25, h: H, fill: { color: C.amber }, line: { type: "none" } });
  s.addShape("rect", { x: 0.25, y: 0, w: 0.08, h: H, fill: { color: C.coral }, line: { type: "none" } });

  s.addText("SIGIL", {
    x: 1, y: 1.8, w: 11, h: 1.6,
    fontFace: F.head, fontSize: 96, bold: true, color: C.paper, charSpacing: 12,
  });
  s.addText("A Hardened Agent OS", {
    x: 1, y: 3.4, w: 11, h: 0.6,
    fontFace: F.head, fontSize: 28, italic: true, color: C.accent,
  });
  // Rule
  s.addShape("rect", { x: 1, y: 4.2, w: 1.4, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  s.addText(
    "State-synchronized kernel. Zero-trust agents. Snapshot & delta.\nPluggable planner. Immutable audit. Checkpointed writes.",
    {
      x: 1, y: 4.45, w: 10, h: 1.2,
      fontFace: F.body, fontSize: 18, color: C.paper, paraSpaceAfter: 6,
    }
  );
  s.addText("Architecture Blueprint  ·  .NET 9 kernel  ·  Microsoft Agent Framework agents", {
    x: 1, y: H - 0.7, w: 11, h: 0.3,
    fontFace: F.body, fontSize: 11, color: C.accent, charSpacing: 3,
  });
  pageNum(s, next(), TOTAL, true);
}

// 2. Vision & Core Evolution
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Vision", "A State-Synchronized Kernel — not another agent framework");

  s.addText(
    "Sigil manages, discovers, orchestrates, and observes remote domain-specific AI agents. " +
    "It sits above Microsoft Agent Framework and provides the OS-level services individual agents shouldn't build themselves.",
    { x: 0.6, y: 2.0, w: 7.8, h: 1.6, fontFace: F.body, fontSize: 16, color: C.body }
  );

  s.addText("Agents are ephemeral workers.\nThe Kernel is the sole source of truth.", {
    x: 0.6, y: 3.7, w: 7.8, h: 1.2,
    fontFace: F.head, fontSize: 22, italic: true, color: C.navy, paraSpaceAfter: 6,
  });

  // Side card — design pillars
  card(s, 9.0, 2.0, 3.8, 4.5, { fill: C.navy });
  s.addText("Design Pillars", {
    x: 9.2, y: 2.15, w: 3.5, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.amber,
  });
  const pillars = [
    ["State", "Snapshot → Delta (no callbacks)"],
    ["Security", "mTLS + JWT + Sigil-Key"],
    ["Resilience", "Pre-flight validation"],
    ["Concurrency", "Optimistic via ETags"],
    ["Audit", "Immutable trail"],
    ["Routing", "Weighted / canary"],
    ["Planning", "Deterministic · LLM · Hybrid"],
  ];
  let py = 2.65;
  pillars.forEach(([k, v]) => {
    s.addText(k, { x: 9.2, y: py, w: 3.5, h: 0.22, fontFace: F.body, fontSize: 10, bold: true, color: C.amber, charSpacing: 2 });
    s.addText(v, { x: 9.2, y: py + 0.22, w: 3.5, h: 0.28, fontFace: F.body, fontSize: 11, color: C.paper });
    py += 0.55;
  });

  pageNum(s, next(), TOTAL);
}

// 3. Core Analogy
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Core Analogy", "Traditional OS concepts mapped to Sigil");

  const rows = [
    ["Processes", "Domain Agents"],
    ["Devices & Drivers", "Tools / APIs / MCP Servers"],
    ["Context Switch", "Context Snapshot"],
    ["System Calls", "Secure Gateway (mTLS/JWT)"],
    ["Virtual Memory", "Atomic Context Bus (ETag)"],
    ["BIOS POST", "Pre-flight Validation"],
    ["Process Scheduler", "Orchestrator"],
    ["Service Registry", "Agent Registry"],
    ["Kernel Policies", "Policy Engine"],
    ["top / htop", "Observability Dashboard"],
  ];

  // Two columns of pairs
  const startY = 2.1, rowH = 0.45;
  const colW = 5.9;
  rows.forEach((r, i) => {
    const col = i < 5 ? 0 : 1;
    const row = i % 5;
    const x = 0.6 + col * (colW + 0.4);
    const y = startY + row * (rowH + 0.2);
    // OS side
    card(s, x, y, colW * 0.42, rowH, { fill: C.navy, stroke: C.navy });
    s.addText(r[0], {
      x: x + 0.1, y, w: colW * 0.42 - 0.2, h: rowH,
      fontFace: F.body, fontSize: 12, color: C.paper, bold: true, valign: "middle",
    });
    // Arrow
    s.addText("→", {
      x: x + colW * 0.42, y, w: 0.4, h: rowH,
      fontFace: F.head, fontSize: 18, color: C.amber, bold: true, valign: "middle", align: "center",
    });
    // Sigil side
    card(s, x + colW * 0.42 + 0.4, y, colW * 0.58 - 0.4, rowH, { fill: C.card, stroke: C.rule });
    s.addText(r[1], {
      x: x + colW * 0.42 + 0.5, y, w: colW * 0.58 - 0.6, h: rowH,
      fontFace: F.body, fontSize: 12, color: C.body, valign: "middle",
    });
  });

  pageNum(s, next(), TOTAL);
}

// 4. Architecture Overview
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Architecture Overview", "Frontend · Kernel · Remote agent containers");

  // Three big lanes
  const laneY = 2.1, laneH = 4.8;
  const lanes = [
    { x: 0.6, w: 3.6, title: "Angular Frontend", fill: C.mid, items: ["Dashboard", "Agent Monitor", "Job Viewer", "Checkpoint Queue", "Intent Console", "Audit Explorer"] },
    { x: 4.45, w: 4.6, title: "Sigil Kernel (.NET 9)", fill: C.navy, items: ["Registry (versions, weights)", "Orchestrator + Snapshot Engine", "Policy Engine", "ISigilStore · IAuditStore", "Security Layer (JWT/mTLS)", "Secure Gateway (Polly)"] },
    { x: 9.3, w: 3.5, title: "Secure Agent Containers", fill: C.deep, items: ["Sigil Agent SDK", "MS Agent Framework", "Tools / APIs", "mTLS + JWT", "/sigil/validate", "/sigil/execute"] },
  ];
  lanes.forEach((L) => {
    card(s, L.x, laneY, L.w, laneH, { fill: L.fill, stroke: L.fill });
    s.addText(L.title, { x: L.x + 0.2, y: laneY + 0.15, w: L.w - 0.4, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.amber });
    s.addShape("rect", { x: L.x + 0.2, y: laneY + 0.55, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
    let y = laneY + 0.75;
    L.items.forEach((it) => {
      s.addText("• " + it, { x: L.x + 0.25, y, w: L.w - 0.5, h: 0.4, fontFace: F.body, fontSize: 12, color: C.paper });
      y += 0.45;
    });
  });

  // Connector labels
  s.addText("SignalR / REST", { x: 4.2, y: 4.0, w: 0.8, h: 0.3, fontFace: F.body, fontSize: 9, italic: true, color: C.mid, align: "center" });
  s.addText("Snapshot →", { x: 9.05, y: 3.6, w: 0.8, h: 0.3, fontFace: F.body, fontSize: 9, italic: true, color: C.mid, align: "center" });
  s.addText("← Delta", { x: 9.05, y: 4.4, w: 0.8, h: 0.3, fontFace: F.body, fontSize: 9, italic: true, color: C.mid, align: "center" });

  pageNum(s, next(), TOTAL);
}

// 5. Agent Protocol — Endpoint Map
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Agent Protocol", "Every remote agent exposes these HTTP endpoints");

  const eps = [
    ["POST", "/sigil/validate", "Pre-flight — can the agent handle this task right now?"],
    ["POST", "/sigil/execute", "Receive Task + Snapshot, return Delta"],
    ["GET",  "/sigil/health", "Liveness + capability status"],
    ["POST", "/sigil/cancel/{taskId}", "Cancel a running task"],
    ["GET",  "/sigil/info", "Agent metadata (id, domain, capabilities, version)"],
  ];
  let y = 2.1;
  eps.forEach((e) => {
    card(s, 0.6, y, 12.2, 0.75);
    s.addShape("rect", { x: 0.6, y, w: 0.18, h: 0.75, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(e[0], { x: 0.95, y, w: 0.8, h: 0.75, fontFace: F.body, fontSize: 12, bold: true, color: C.navy, valign: "middle", charSpacing: 2 });
    s.addText(e[1], { x: 1.75, y, w: 3.5, h: 0.75, fontFace: "Consolas", fontSize: 14, bold: true, color: C.deep, valign: "middle" });
    s.addText(e[2], { x: 5.3, y, w: 7.3, h: 0.75, fontFace: F.body, fontSize: 13, color: C.body, valign: "middle" });
    y += 0.9;
  });

  pageNum(s, next(), TOTAL);
}

// 6. Pre-flight Validation
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Pre-flight Validation", "Prevents zombie tasks — agents accepting work they can't finish");

  // Left — flow steps
  const steps = [
    ["1", "Orchestrator", "evaluates policy (budget, access)"],
    ["2", "Gateway", "POST /sigil/validate with task preview"],
    ["3", "Agent", "returns ValidationResult (canHandle, tokens, missingTools)"],
    ["4a", "Passed", "dispatch with Snapshot"],
    ["4b", "Failed", "try next agent or fail job"],
  ];
  let y = 2.1;
  steps.forEach((st) => {
    const isAlt = st[0].startsWith("4");
    card(s, 0.6, y, 7.5, 0.7, { fill: isAlt ? C.card : C.card, stroke: C.rule });
    iconDot(s, 0.8, y + 0.19, st[0] === "4b" ? C.coral : C.navy);
    s.addText(st[0], { x: 0.8, y: y + 0.19, w: 0.32, h: 0.32, fontFace: F.body, fontSize: 11, bold: true, color: C.paper, align: "center", valign: "middle" });
    s.addText(st[1], { x: 1.3, y, w: 2, h: 0.7, fontFace: F.body, fontSize: 13, bold: true, color: C.navy, valign: "middle" });
    s.addText(st[2], { x: 3.3, y, w: 4.7, h: 0.7, fontFace: F.body, fontSize: 12, color: C.body, valign: "middle" });
    y += 0.82;
  });

  // Right — contract
  card(s, 8.5, 2.1, 4.3, 4.5, { fill: C.deep, stroke: C.deep });
  s.addText("ValidationResult", { x: 8.7, y: 2.25, w: 4, h: 0.35, fontFace: F.head, fontSize: 15, bold: true, color: C.amber });
  s.addShape("rect", { x: 8.7, y: 2.62, w: 0.7, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  const fields = [
    ["bool", "CanHandle"],
    ["int", "EstimatedTokens"],
    ["string[]", "MissingTools"],
    ["string?", "Reason"],
  ];
  let fy = 2.8;
  fields.forEach(([t, nm]) => {
    s.addText(t, { x: 8.7, y: fy, w: 1.4, h: 0.35, fontFace: "Consolas", fontSize: 13, color: C.accent });
    s.addText(nm, { x: 10.1, y: fy, w: 2.7, h: 0.35, fontFace: "Consolas", fontSize: 13, bold: true, color: C.paper });
    fy += 0.5;
  });
  s.addText("Orchestrator uses estimates to enforce token budget before dispatch.", {
    x: 8.7, y: 5.3, w: 4, h: 1.1, fontFace: F.body, fontSize: 11, italic: true, color: C.accent,
  });

  pageNum(s, next(), TOTAL);
}

// 7. Snapshot & Delta
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Snapshot & Delta", "Solves the chatty-context problem — zero callbacks to the kernel");

  // Flow across the slide
  const cols = [
    { label: "Kernel", sub: "GetSnapshot(jobId)", fill: C.navy },
    { label: "Policy", sub: "scrub PII · inject creds", fill: C.mid },
    { label: "Agent", sub: "/sigil/execute\nprocesses locally", fill: C.deep },
    { label: "Context Bus", sub: "CommitDelta(ETag)", fill: C.navy },
  ];
  const cw = 2.6, cy = 2.2, ch = 2.0;
  cols.forEach((c, i) => {
    const x = 0.6 + i * (cw + 0.35);
    card(s, x, cy, cw, ch, { fill: c.fill, stroke: c.fill });
    s.addText(c.label, { x: x + 0.15, y: cy + 0.2, w: cw - 0.3, h: 0.5, fontFace: F.head, fontSize: 18, bold: true, color: C.amber });
    s.addText(c.sub, { x: x + 0.15, y: cy + 0.85, w: cw - 0.3, h: 1.0, fontFace: F.body, fontSize: 12, color: C.paper });
    if (i < cols.length - 1) {
      s.addText("▶", { x: x + cw, y: cy + 0.85, w: 0.35, h: 0.3, fontFace: F.body, fontSize: 18, color: C.amber, align: "center" });
    }
  });

  // Bottom — two-column spec
  card(s, 0.6, 4.7, 5.95, 2.1, { fill: C.card });
  s.addShape("rect", { x: 0.6, y: 4.7, w: 0.12, h: 2.1, fill: { color: C.amber }, line: { type: "none" } });
  s.addText("AgentExecutionPackage (to agent)", { x: 0.85, y: 4.8, w: 5.6, h: 0.35, fontFace: F.head, fontSize: 13, bold: true, color: C.navy });
  s.addText(
    "• Task\n• ContextSnapshot (key/value)\n• ETag (concurrency token)\n• ScopedCredentials (per-tool)",
    { x: 0.85, y: 5.2, w: 5.6, h: 1.6, fontFace: "Consolas", fontSize: 12, color: C.body, paraSpaceAfter: 2 }
  );

  card(s, 6.85, 4.7, 5.95, 2.1, { fill: C.card });
  s.addShape("rect", { x: 6.85, y: 4.7, w: 0.12, h: 2.1, fill: { color: C.coral }, line: { type: "none" } });
  s.addText("AgentExecutionResult (from agent)", { x: 7.1, y: 4.8, w: 5.6, h: 0.35, fontFace: F.head, fontSize: 13, bold: true, color: C.navy });
  s.addText(
    "• TaskId, Success\n• StateUpdates (delta)\n• Logs (structured)\n• UsageMetrics (tokens, duration)",
    { x: 7.1, y: 5.2, w: 5.6, h: 1.6, fontFace: "Consolas", fontSize: 12, color: C.body }
  );

  pageNum(s, next(), TOTAL);
}

// 8. Secure Agent Registry
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Secure Agent Registry", "Registration · weighted routing · lifecycle");

  // Lifecycle flow (horizontal)
  const states = [
    { label: "Starting", color: C.muted },
    { label: "Healthy", color: C.navy },
    { label: "Degraded", color: C.amber },
    { label: "Offline", color: C.coral },
    { label: "Draining", color: C.mid },
  ];
  const sx = 0.6, sy = 2.15, sw = 2.25, sh = 0.9;
  states.forEach((st, i) => {
    const x = sx + i * (sw + 0.18);
    card(s, x, sy, sw, sh, { fill: st.color, stroke: st.color });
    s.addText(st.label, { x, y: sy, w: sw, h: sh, fontFace: F.head, fontSize: 16, bold: true, color: C.paper, align: "center", valign: "middle" });
  });
  s.addText("3 missed heartbeats → Offline     ·     Heartbeat every 15s     ·     Stale cleanup on extended offline", {
    x: 0.6, y: sy + sh + 0.15, w: 12.2, h: 0.35, fontFace: F.body, fontSize: 11, italic: true, color: C.mid, align: "center",
  });

  // Weighted routing visual
  card(s, 0.6, 4.3, 6, 2.6);
  s.addText("Weighted Routing", { x: 0.8, y: 4.4, w: 5.6, h: 0.35, fontFace: F.head, fontSize: 15, bold: true, color: C.navy });
  s.addShape("rect", { x: 0.8, y: 4.75, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  // Stable 90 / Canary 10 bar
  s.addText("Stable agent · weight 90", { x: 0.8, y: 4.9, w: 5, h: 0.3, fontFace: F.body, fontSize: 11, color: C.body });
  s.addShape("rect", { x: 0.8, y: 5.2, w: 5, h: 0.35, fill: { color: C.navy }, line: { type: "none" } });
  s.addText("Canary agent · weight 10", { x: 0.8, y: 5.7, w: 5, h: 0.3, fontFace: F.body, fontSize: 11, color: C.body });
  s.addShape("rect", { x: 0.8, y: 6.0, w: 0.56, h: 0.35, fill: { color: C.amber }, line: { type: "none" } });
  s.addText("A/B and canary deployments with no code changes.", { x: 0.8, y: 6.45, w: 5.6, h: 0.35, fontFace: F.body, fontSize: 10, italic: true, color: C.muted });

  // Security tiers
  card(s, 6.85, 4.3, 5.95, 2.6);
  s.addText("Security Tiers", { x: 7.05, y: 4.4, w: 5.6, h: 0.35, fontFace: F.head, fontSize: 15, bold: true, color: C.navy });
  s.addShape("rect", { x: 7.05, y: 4.75, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  const tiers = [
    ["Open", "Sigil-Key", "No PII"],
    ["Standard", "Sigil-Key + JWT", "No PII"],
    ["Trusted", "mTLS + JWT", "PII-Cleared"],
  ];
  let ty = 4.9;
  tiers.forEach(([a, b, c]) => {
    s.addText(a, { x: 7.05, y: ty, w: 1.5, h: 0.4, fontFace: F.body, fontSize: 12, bold: true, color: C.navy, valign: "middle" });
    s.addText(b, { x: 8.55, y: ty, w: 2.5, h: 0.4, fontFace: "Consolas", fontSize: 11, color: C.body, valign: "middle" });
    s.addText(c, { x: 11.05, y: ty, w: 1.7, h: 0.4, fontFace: F.body, fontSize: 11, italic: true, color: C.mid, valign: "middle" });
    ty += 0.5;
  });

  pageNum(s, next(), TOTAL);
}

// 9. Planner — Strategy
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Planner", "IPlanner strategy — decomposes intent into an execution plan");

  const modes = [
    { name: "Deterministic", sub: "Capability match + weighted selection", detail: "Zero LLM dependency.\nShips with Sigil Core.", fill: C.navy },
    { name: "Hybrid", sub: "Deterministic first, LLM fallback", detail: "Recommended default.\nEscalates when ambiguous.", fill: C.amber, textDark: true },
    { name: "LLM-Only", sub: "IChatClient decomposition", detail: "System prompt built from\nthe live registry.", fill: C.deep },
  ];
  const mw = 4.05, my = 2.1, mh = 3.3;
  modes.forEach((m, i) => {
    const x = 0.6 + i * (mw + 0.2);
    card(s, x, my, mw, mh, { fill: m.fill, stroke: m.fill });
    const titleColor = m.textDark ? C.deep : C.amber;
    const bodyColor = m.textDark ? C.deep : C.paper;
    s.addText(m.name, { x: x + 0.25, y: my + 0.25, w: mw - 0.5, h: 0.5, fontFace: F.head, fontSize: 22, bold: true, color: titleColor });
    s.addShape("rect", { x: x + 0.25, y: my + 0.85, w: 0.8, h: 0.04, fill: { color: m.textDark ? C.deep : C.paper }, line: { type: "none" } });
    s.addText(m.sub, { x: x + 0.25, y: my + 1.0, w: mw - 0.5, h: 0.7, fontFace: F.body, fontSize: 13, italic: true, color: bodyColor });
    s.addText(m.detail, { x: x + 0.25, y: my + 1.85, w: mw - 0.5, h: 1.3, fontFace: F.body, fontSize: 12, color: bodyColor });
  });

  // Bottom — IChatClient providers
  card(s, 0.6, 5.65, 12.2, 1.25);
  s.addText("IChatClient · provider-agnostic (Microsoft.Extensions.AI)", { x: 0.8, y: 5.72, w: 8, h: 0.35, fontFace: F.head, fontSize: 13, bold: true, color: C.navy });
  const provs = ["Anthropic (Claude)", "Azure OpenAI", "OpenAI", "Ollama (local)"];
  provs.forEach((p, i) => {
    const x = 0.8 + i * 3;
    s.addShape("rect", { x, y: 6.2, w: 0.1, h: 0.4, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(p, { x: x + 0.2, y: 6.2, w: 2.7, h: 0.4, fontFace: F.body, fontSize: 12, color: C.body, valign: "middle" });
  });

  pageNum(s, next(), TOTAL);
}

// 10. Orchestrator Flow
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Orchestrator", "Plan · validate · snapshot · dispatch · commit · audit");

  const steps = [
    ["Submit Intent", "User or API"],
    ["Plan", "IPlanner → ExecutionPlan"],
    ["Pre-flight", "Policy evaluation + scoped creds"],
    ["Validate", "POST /sigil/validate"],
    ["Snapshot", "GetSnapshotAsync + ETag"],
    ["Dispatch", "POST /sigil/execute"],
    ["Commit Delta", "CommitDeltaAsync(ETag)"],
    ["Audit", "LogChangeAsync (immutable)"],
  ];

  // 2 rows of 4
  const cw = 2.85, ch = 2.0;
  steps.forEach((st, i) => {
    const col = i % 4, row = Math.floor(i / 4);
    const x = 0.6 + col * (cw + 0.23);
    const y = 2.2 + row * (ch + 0.3);
    card(s, x, y, cw, ch);
    s.addShape("rect", { x, y, w: cw, h: 0.12, fill: { color: i < 4 ? C.navy : C.mid }, line: { type: "none" } });
    s.addText(String(i + 1).padStart(2, "0"), {
      x: x + 0.2, y: y + 0.25, w: 0.8, h: 0.4,
      fontFace: F.head, fontSize: 18, bold: true, color: C.amber, charSpacing: 2,
    });
    s.addText(st[0], { x: x + 0.2, y: y + 0.7, w: cw - 0.4, h: 0.45, fontFace: F.head, fontSize: 15, bold: true, color: C.navy });
    s.addText(st[1], { x: x + 0.2, y: y + 1.2, w: cw - 0.4, h: 0.7, fontFace: F.body, fontSize: 11, color: C.body });
  });

  pageNum(s, next(), TOTAL);
}

// 11. Atomic Context Bus
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Atomic Context Bus", "Optimistic concurrency via ETags — no distributed locks");

  // Two-column: interface + race example
  card(s, 0.6, 2.1, 6.1, 4.8);
  s.addShape("rect", { x: 0.6, y: 2.1, w: 0.12, h: 4.8, fill: { color: C.navy }, line: { type: "none" } });
  s.addText("IContextStore", { x: 0.9, y: 2.2, w: 5.8, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.navy });
  s.addText(
    "GetSnapshotAsync(jobId)\n   → (Snapshot, ETag)\n\n" +
    "CommitDeltaAsync(jobId, delta, expectedETag)\n   → bool (true if ETag matched)\n\n" +
    "AppendLogAsync(jobId, entry)\n\n" +
    "GetLogAsync(jobId)\n   → IReadOnlyList<AgentLogEntry>",
    { x: 0.9, y: 2.7, w: 5.8, h: 4.1, fontFace: "Consolas", fontSize: 12, color: C.body, paraSpaceAfter: 4 }
  );

  // Right — race example
  card(s, 6.85, 2.1, 5.95, 4.8, { fill: C.deep, stroke: C.deep });
  s.addText("Concurrent writers", { x: 7.05, y: 2.2, w: 5.5, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.amber });
  s.addShape("rect", { x: 7.05, y: 2.6, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });

  const events = [
    ["State", "{ count: 1 }  ETag: abc"],
    ["Agent A reads", "ETag: abc"],
    ["Agent B reads", "ETag: abc"],
    ["A commits → ✓", "new ETag: def"],
    ["B commits → ✗", "conflict (expected abc)"],
    ["Orchestrator", "retries B with fresh snapshot"],
  ];
  let ey = 2.85;
  events.forEach(([k, v], i) => {
    const color = i === 4 ? C.coral : i === 3 ? C.amber : C.accent;
    s.addShape("rect", { x: 7.05, y: ey + 0.1, w: 0.1, h: 0.4, fill: { color }, line: { type: "none" } });
    s.addText(k, { x: 7.25, y: ey, w: 2, h: 0.5, fontFace: F.body, fontSize: 11, bold: true, color: C.paper, valign: "middle" });
    s.addText(v, { x: 9.3, y: ey, w: 3.4, h: 0.5, fontFace: "Consolas", fontSize: 11, color: C.accent, valign: "middle" });
    ey += 0.6;
  });

  pageNum(s, next(), TOTAL);
}

// 12. Zero-Trust Policy Engine
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Policy Engine", "Enforced pre-flight — before the agent ever receives work");

  const policies = [
    ["Token Budget", "Pre-flight", "Estimated cost vs remaining budget"],
    ["Tool Access", "Pre-flight", "Scoped credentials per step only"],
    ["PII Masking", "Pre-flight", "Scrub snapshot if !IsPiiCleared"],
    ["Checkpoint", "Pre-flight", "Human approval for writes"],
    ["Rate Limiting", "Pre-flight", "Concurrent jobs · requests/min"],
    ["Timeout", "Dispatch", "Polly — per step, per job"],
    ["Retry", "Dispatch", "Polly circuit breaker"],
  ];
  let y = 2.05;
  policies.forEach((p) => {
    card(s, 0.6, y, 12.2, 0.62);
    s.addShape("rect", { x: 0.6, y, w: 0.12, h: 0.62, fill: { color: p[1] === "Dispatch" ? C.mid : C.navy }, line: { type: "none" } });
    s.addText(p[0], { x: 0.85, y, w: 3, h: 0.62, fontFace: F.body, fontSize: 13, bold: true, color: C.navy, valign: "middle" });
    // Stage pill
    const pillColor = p[1] === "Dispatch" ? C.mid : C.navy;
    s.addShape("roundRect", { x: 3.9, y: y + 0.16, w: 1.2, h: 0.3, fill: { color: pillColor }, line: { type: "none" }, rectRadius: 0.15 });
    s.addText(p[1], { x: 3.9, y: y + 0.16, w: 1.2, h: 0.3, fontFace: F.body, fontSize: 9, bold: true, color: C.paper, align: "center", valign: "middle", charSpacing: 2 });
    s.addText(p[2], { x: 5.3, y, w: 7.3, h: 0.62, fontFace: F.body, fontSize: 12, color: C.body, valign: "middle" });
    y += 0.7;
  });

  pageNum(s, next(), TOTAL);
}

// 13. Checkpoint pattern
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Checkpoints", "Non-negotiable for writes — human-in-the-loop for all mutations");

  const flow = [
    "Agent returns delta requesting a write",
    "Policy Engine evaluates checkpoint policy",
    "Job status → Paused",
    "SignalR pushes approval card to UI",
    "User approves or rejects",
    "Resume and commit — or abort",
  ];
  let y = 2.1;
  flow.forEach((t, i) => {
    card(s, 0.6, y, 8.5, 0.62);
    s.addShape("ellipse", { x: 0.75, y: y + 0.13, w: 0.36, h: 0.36, fill: { color: C.navy }, line: { type: "none" } });
    s.addText(String(i + 1), { x: 0.75, y: y + 0.13, w: 0.36, h: 0.36, fontFace: F.body, fontSize: 11, bold: true, color: C.amber, align: "center", valign: "middle" });
    s.addText(t, { x: 1.3, y, w: 7.5, h: 0.62, fontFace: F.body, fontSize: 13, color: C.body, valign: "middle" });
    y += 0.72;
  });

  // Right — big callout
  card(s, 9.4, 2.1, 3.4, 4.6, { fill: C.coral, stroke: C.coral });
  s.addText("Writes\nPause.", { x: 9.55, y: 2.35, w: 3.2, h: 1.8, fontFace: F.head, fontSize: 44, bold: true, color: C.paper });
  s.addShape("rect", { x: 9.55, y: 4.2, w: 1.0, h: 0.06, fill: { color: C.paper }, line: { type: "none" } });
  s.addText(
    "Any write-side capability runs through the checkpoint policy. The job waits on a SignalR-delivered approval before committing.",
    { x: 9.55, y: 4.4, w: 3.1, h: 2.2, fontFace: F.body, fontSize: 13, italic: true, color: C.paper }
  );

  pageNum(s, next(), TOTAL);
}

// 14. Security Model
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Security Model", "Zero-trust — all traffic authenticated and signed");

  // Flow top strip
  const steps = [
    ["Register", "Sigil-Key or mTLS cert → short-lived Agent-JWT"],
    ["Execute", "Kernel signs dispatch · Agent verifies · Delta signed with JWT"],
    ["Refresh", "SDK rotates JWT before expiry"],
  ];
  let x = 0.6;
  const sw = 4.05;
  steps.forEach((st, i) => {
    card(s, x, 2.1, sw, 1.6, { fill: C.navy, stroke: C.navy });
    s.addText(st[0], { x: x + 0.25, y: 2.2, w: sw - 0.5, h: 0.4, fontFace: F.head, fontSize: 18, bold: true, color: C.amber });
    s.addShape("rect", { x: x + 0.25, y: 2.68, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(st[1], { x: x + 0.25, y: 2.8, w: sw - 0.5, h: 0.9, fontFace: F.body, fontSize: 12, color: C.paper });
    x += sw + 0.2;
  });

  // Tier table
  card(s, 0.6, 4.1, 12.2, 2.8);
  s.addText("Tiered access", { x: 0.8, y: 4.2, w: 6, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.navy });
  s.addShape("rect", { x: 0.8, y: 4.6, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });

  const rows = [
    ["Tier", "Auth", "PII", "Use case"],
    ["Open", "Sigil-Key", "No", "Dev / local agents"],
    ["Standard", "Sigil-Key + JWT", "No", "Prod agents without sensitive data"],
    ["Trusted", "mTLS + JWT", "Yes (PII-Cleared)", "Agents handling personal data"],
  ];
  const colX = [0.8, 2.3, 4.9, 6.6];
  const colW = [1.5, 2.6, 1.7, 6.0];
  let ry = 4.8;
  rows.forEach((r, ri) => {
    r.forEach((c, ci) => {
      s.addText(c, {
        x: colX[ci], y: ry, w: colW[ci], h: 0.4,
        fontFace: F.body, fontSize: ri === 0 ? 10 : 12,
        bold: ri === 0 || ci === 0, color: ri === 0 ? C.muted : C.body,
        charSpacing: ri === 0 ? 2 : 0, valign: "middle",
      });
    });
    if (ri === 0) {
      s.addShape("rect", { x: 0.8, y: ry + 0.38, w: 12, h: 0.02, fill: { color: C.rule }, line: { type: "none" } });
    }
    ry += 0.5;
  });

  pageNum(s, next(), TOTAL);
}

// 15. Storage abstraction
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Storage Abstraction", "ISigilStore · two providers · zero kernel dependency on a database");

  // Center — ISigilStore hub
  const hubX = 5.5, hubY = 2.6, hubW = 2.3, hubH = 2.3;
  card(s, hubX, hubY, hubW, hubH, { fill: C.navy, stroke: C.navy });
  s.addText("ISigilStore", { x: hubX, y: hubY + 0.2, w: hubW, h: 0.5, fontFace: F.head, fontSize: 18, bold: true, color: C.amber, align: "center" });
  s.addShape("rect", { x: hubX + hubW / 2 - 0.4, y: hubY + 0.75, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  s.addText("Agents\nJobs\nContexts\nCheckpoints\nAudit", { x: hubX + 0.2, y: hubY + 0.85, w: hubW - 0.4, h: 1.4, fontFace: F.body, fontSize: 12, color: C.paper, align: "center", paraSpaceAfter: 2 });

  // Left provider — Mongo
  card(s, 0.6, 2.6, 4.5, 2.3);
  s.addShape("rect", { x: 0.6, y: 2.6, w: 0.12, h: 2.3, fill: { color: C.mid }, line: { type: "none" } });
  s.addText("Sigil.Storage.Mongo", { x: 0.85, y: 2.75, w: 4, h: 0.4, fontFace: F.head, fontSize: 15, bold: true, color: C.navy });
  s.addText("MongoSigilStore\nMongoAuditStore\n\n.UseMongo(connectionString, database)", { x: 0.85, y: 3.25, w: 4, h: 1.6, fontFace: "Consolas", fontSize: 11, color: C.body });

  // Right provider — EF
  card(s, 8.2, 2.6, 4.6, 2.3);
  s.addShape("rect", { x: 8.2, y: 2.6, w: 0.12, h: 2.3, fill: { color: C.mid }, line: { type: "none" } });
  s.addText("Sigil.Storage.EfCore", { x: 8.45, y: 2.75, w: 4.2, h: 0.4, fontFace: F.head, fontSize: 15, bold: true, color: C.navy });
  s.addText("EfSigilStore · EfAuditStore\nSigilDbContext · Migrations\n\n.UseEfCore(options => ...)", { x: 8.45, y: 3.25, w: 4.2, h: 1.6, fontFace: "Consolas", fontSize: 11, color: C.body });

  // Connectors
  s.addShape("rect", { x: 5.1, y: 3.7, w: 0.4, h: 0.06, fill: { color: C.rule }, line: { type: "none" } });
  s.addShape("rect", { x: 7.8, y: 3.7, w: 0.4, h: 0.06, fill: { color: C.rule }, line: { type: "none" } });

  // Audit callout
  card(s, 0.6, 5.2, 12.2, 1.7, { fill: C.deep, stroke: C.deep });
  s.addText("IAuditStore — immutable", { x: 0.85, y: 5.35, w: 6, h: 0.4, fontFace: F.head, fontSize: 15, bold: true, color: C.amber });
  s.addShape("rect", { x: 0.85, y: 5.75, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  s.addText("Every context change writes an AuditEntry: JobId · AgentId · StepId · Delta · Metrics · Timestamp. Never mutated. Never deleted.", {
    x: 0.85, y: 5.9, w: 12, h: 0.9, fontFace: F.body, fontSize: 12, italic: true, color: C.paper,
  });

  pageNum(s, next(), TOTAL);
}

// 16. Observability
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Observability", "OpenTelemetry · structured logs in deltas · cost tracking");

  // Pillars
  const p = [
    { k: "Traces", v: "Job → Planner → Step → Tool span hierarchy via System.Diagnostics.Activity", c: C.navy },
    { k: "Logs", v: "Structured JSON returned in the Delta package by each agent", c: C.mid },
    { k: "Metrics", v: "Cost-per-intent, token usage, latency, success rate → Prometheus / Grafana", c: C.deep },
  ];
  const pw = 4.05, py = 2.1, ph = 2.3;
  p.forEach((pp, i) => {
    const x = 0.6 + i * (pw + 0.2);
    card(s, x, py, pw, ph, { fill: pp.c, stroke: pp.c });
    s.addText(pp.k, { x: x + 0.25, y: py + 0.25, w: pw - 0.5, h: 0.5, fontFace: F.head, fontSize: 22, bold: true, color: C.amber });
    s.addShape("rect", { x: x + 0.25, y: py + 0.85, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(pp.v, { x: x + 0.25, y: py + 1.0, w: pw - 0.5, h: 1.2, fontFace: F.body, fontSize: 12, color: C.paper });
  });

  // Trace hierarchy mock
  card(s, 0.6, 4.65, 12.2, 2.25);
  s.addText("Trace hierarchy", { x: 0.8, y: 4.75, w: 6, h: 0.35, fontFace: F.head, fontSize: 14, bold: true, color: C.navy });
  s.addShape("rect", { x: 0.8, y: 5.1, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });

  const bars = [
    { label: "Job (root)", x: 0.8, w: 11.8, color: C.navy, y: 5.25 },
    { label: "Planner.Plan", x: 1.1, w: 2.0, color: C.mid, y: 5.65 },
    { label: "Policy.PreFlight", x: 3.2, w: 1.2, color: C.mid, y: 5.65 },
    { label: "Agent.Validate", x: 4.5, w: 1.2, color: C.mid, y: 5.65 },
    { label: "Context.Snapshot", x: 5.8, w: 1.4, color: C.mid, y: 5.65 },
    { label: "Agent.Execute", x: 7.3, w: 3.6, color: C.mid, y: 5.65 },
    { label: "Commit+Audit", x: 11.0, w: 1.55, color: C.mid, y: 5.65 },
    { label: "Tool.Create", x: 7.5, w: 1.5, color: C.amber, y: 6.15 },
    { label: "Tool.Send", x: 9.2, w: 1.5, color: C.amber, y: 6.15 },
  ];
  bars.forEach((b) => {
    s.addShape("rect", { x: b.x, y: b.y, w: b.w, h: 0.3, fill: { color: b.color }, line: { type: "none" } });
    s.addText(b.label, { x: b.x + 0.05, y: b.y, w: b.w - 0.1, h: 0.3, fontFace: F.body, fontSize: 9, color: C.paper, valign: "middle" });
  });

  pageNum(s, next(), TOTAL);
}

// 17. Agent SDK
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Agent SDK", "Agent authors write domain logic — the SDK handles everything else");

  // Left — what the SDK handles
  card(s, 0.6, 2.1, 5.9, 4.8, { fill: C.navy, stroke: C.navy });
  s.addText("Handled by the SDK", { x: 0.85, y: 2.25, w: 5.5, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.amber });
  s.addShape("rect", { x: 0.85, y: 2.65, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  const items = [
    "Self-registration (Sigil-Key or mTLS)",
    "Heartbeat + deregistration",
    "Short-lived JWT — auto refresh",
    "/sigil/validate endpoint",
    "/sigil/execute endpoint",
    "Snapshot / Delta plumbing",
    "Request signing + verification",
  ];
  let iy = 2.85;
  items.forEach((it) => {
    s.addShape("rect", { x: 0.85, y: iy + 0.18, w: 0.12, h: 0.12, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(it, { x: 1.1, y: iy, w: 5.2, h: 0.5, fontFace: F.body, fontSize: 13, color: C.paper, valign: "middle" });
    iy += 0.52;
  });

  // Right — handler code sketch
  card(s, 6.7, 2.1, 6.1, 4.8, { fill: C.deep, stroke: C.deep });
  s.addText("What the agent author writes", { x: 6.95, y: 2.25, w: 5.5, h: 0.4, fontFace: F.head, fontSize: 16, bold: true, color: C.amber });
  s.addShape("rect", { x: 6.95, y: 2.65, w: 0.8, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });
  s.addText(
    "public class ResearchHandler : ISigilAgentHandler\n" +
    "{\n" +
    "  public async Task<AgentExecutionResult> ExecuteAsync(\n" +
    "    AgentExecutionPackage package, CancellationToken ct)\n" +
    "  {\n" +
    "    var topic = package.ContextSnapshot\n" +
    "      .Get<string>(\"topic\");\n" +
    "    var summary = await _researcher\n" +
    "      .ResearchAsync(topic, ct);\n" +
    "    return new AgentExecutionResult {\n" +
    "      Success = true,\n" +
    "      StateUpdates = { [\"summary\"] = summary }\n" +
    "    };\n" +
    "  }\n" +
    "}",
    { x: 6.95, y: 2.8, w: 5.7, h: 4.0, fontFace: "Consolas", fontSize: 11, color: C.accent, paraSpaceAfter: 0 }
  );

  pageNum(s, next(), TOTAL);
}

// 18. Angular Frontend
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Angular Frontend", "The operational dashboard — desktop environment of the Agent OS");

  const views = [
    ["Dashboard", "Active jobs · agent health grid · cost overview"],
    ["Agent Catalog", "Capabilities · security tier · routing weight"],
    ["Job Monitor", "Trace waterfall · snapshot/delta inspector"],
    ["Job History", "Search and replay past jobs with full audit"],
    ["Checkpoint Queue", "Pending human approvals for writes"],
    ["Intent Console", "Submit intents · watch execution live"],
    ["Audit Explorer", "Immutable change history — who, what, when"],
  ];
  let y = 2.1;
  views.forEach((v, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 0.6 + col * 6.2;
    const cy = 2.1 + row * 1.05;
    card(s, x, cy, 5.9, 0.9);
    s.addShape("rect", { x, y: cy, w: 0.12, h: 0.9, fill: { color: i === 4 ? C.coral : C.navy }, line: { type: "none" } });
    s.addText(v[0], { x: x + 0.3, y: cy, w: 2.0, h: 0.9, fontFace: F.head, fontSize: 14, bold: true, color: C.navy, valign: "middle" });
    s.addText(v[1], { x: x + 2.3, y: cy, w: 3.5, h: 0.9, fontFace: F.body, fontSize: 11, color: C.body, valign: "middle" });
  });

  // Tech stack footer
  s.addText("Angular · standalone components · signals · SpartanNG · Tailwind · SignalR live updates", {
    x: 0.6, y: 6.4, w: 12.2, h: 0.4, fontFace: F.body, fontSize: 11, italic: true, color: C.mid, align: "center",
  });

  pageNum(s, next(), TOTAL);
}

// 19. Project Structure
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Project Structure", "Seven libraries under src/ · agents and UI alongside");

  const projs = [
    ["Sigil.Core", "Zero-dependency contracts: protocol, stores, policy, planner", C.navy],
    ["Sigil.Agent.SDK", "NuGet for agent authors — register, heartbeat, validate, snapshot/delta", C.mid],
    ["Sigil.Storage.Mongo", "MongoSigilStore + MongoAuditStore", C.mid],
    ["Sigil.Storage.EfCore", "EfSigilStore + migrations", C.mid],
    ["Sigil.Infrastructure", "Gateway, JWT/mTLS, observability primitives", C.mid],
    ["Sigil.Runtime", "Registry, Orchestrator, SnapshotEngine, Planners, Policies", C.mid],
    ["Sigil.Api", "FastEndpoints + SignalR hubs", C.navy],
    ["agents/", "Sample agents (Echo, Weather, …) using the SDK", C.muted],
    ["sigil-ui/", "Angular dashboard", C.muted],
  ];
  let y = 2.1;
  projs.forEach((p) => {
    card(s, 0.6, y, 12.2, 0.52);
    s.addShape("rect", { x: 0.6, y, w: 0.12, h: 0.52, fill: { color: p[2] }, line: { type: "none" } });
    s.addText(p[0], { x: 0.9, y, w: 3.4, h: 0.52, fontFace: "Consolas", fontSize: 13, bold: true, color: C.navy, valign: "middle" });
    s.addText(p[1], { x: 4.4, y, w: 8.2, h: 0.52, fontFace: F.body, fontSize: 12, color: C.body, valign: "middle" });
    y += 0.58;
  });

  pageNum(s, next(), TOTAL);
}

// 20. Key Design Decisions
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Key Design Decisions", "Where the architecture made a specific, load-bearing choice");

  const decisions = [
    ["Agent hosting", "Out-of-process remote containers"],
    ["State model", "Snapshot & Delta (push-execute-diff)"],
    ["Concurrency", "Optimistic via ETag"],
    ["Security", "Zero-trust: mTLS + JWT + Sigil-Key"],
    ["Pre-flight", "/sigil/validate before dispatch"],
    ["Routing", "Weighted for canary / A-B"],
    ["Audit", "Immutable IAuditStore"],
    ["Storage", "Abstracted · Mongo + EF Core"],
    ["Planner", "IPlanner strategy in Core"],
    ["LLM", "IChatClient (MS.Extensions.AI)"],
    ["Checkpoints", "Non-negotiable for writes"],
    ["Observability", "OTel + structured delta logs"],
  ];
  const cw = 6.05, ch = 0.55;
  decisions.forEach((d, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 0.6 + col * (cw + 0.1);
    const y = 2.1 + row * (ch + 0.15);
    card(s, x, y, cw, ch);
    s.addShape("rect", { x, y, w: 0.12, h: ch, fill: { color: C.amber }, line: { type: "none" } });
    s.addText(d[0], { x: x + 0.3, y, w: 2.1, h: ch, fontFace: F.body, fontSize: 12, bold: true, color: C.navy, valign: "middle" });
    s.addText(d[1], { x: x + 2.45, y, w: cw - 2.6, h: ch, fontFace: F.body, fontSize: 12, color: C.body, valign: "middle" });
  });

  pageNum(s, next(), TOTAL);
}

// 21. Phase Plan
{
  const s = pres.addSlide();
  bg(s, C.paper);
  motif(s);
  title(s, "Phase Plan", "Foundation → Orchestration → Policy → Observability → UI → Polish");

  const phases = [
    { name: "Phase 1", title: "Foundation + Security", detail: "Solution scaffold · stores · protocol · registry · Echo agent · Docker Compose" },
    { name: "Phase 2", title: "Orchestration & Planner", detail: "IPlanner (Det / LLM / Hybrid) · Snapshot engine · ETag commit · audit" },
    { name: "Phase 3", title: "Policy & Zero-Trust", detail: "Policy pipeline · token/tool/PII/checkpoint · JWT + mTLS · Polly" },
    { name: "Phase 4", title: "Observability", detail: "OTel · cost metrics · job traces · structured logs" },
    { name: "Phase 5", title: "Angular Frontend", detail: "Dashboard · catalog · monitor · audit · checkpoints · intent console" },
    { name: "Phase 6", title: "Polish & Extend", detail: "Canary routing · parallel execution · MCP tools · Prometheus export" },
  ];
  let y = 2.1;
  phases.forEach((p, i) => {
    card(s, 0.6, y, 12.2, 0.7);
    s.addShape("rect", { x: 0.6, y, w: 0.12, h: 0.7, fill: { color: i === 0 ? C.amber : C.navy }, line: { type: "none" } });
    s.addText(p.name, { x: 0.85, y, w: 1.3, h: 0.7, fontFace: F.head, fontSize: 14, bold: true, color: C.navy, valign: "middle", charSpacing: 2 });
    s.addText(p.title, { x: 2.2, y, w: 3, h: 0.7, fontFace: F.body, fontSize: 13, bold: true, color: C.deep, valign: "middle" });
    s.addText(p.detail, { x: 5.3, y, w: 7.3, h: 0.7, fontFace: F.body, fontSize: 11, color: C.body, valign: "middle" });
    y += 0.8;
  });

  pageNum(s, next(), TOTAL);
}

// 22. Closing
{
  const s = pres.addSlide();
  bg(s, C.deep);
  s.addShape("rect", { x: 0, y: 0, w: 0.25, h: H, fill: { color: C.amber }, line: { type: "none" } });
  s.addShape("rect", { x: 0.25, y: 0, w: 0.08, h: H, fill: { color: C.coral }, line: { type: "none" } });

  s.addText("A mark of power\nand binding.", {
    x: 1, y: 1.6, w: 11, h: 3, fontFace: F.head, fontSize: 72, bold: true, color: C.paper, italic: true,
  });
  s.addShape("rect", { x: 1, y: 4.5, w: 1.4, h: 0.04, fill: { color: C.amber }, line: { type: "none" } });

  s.addText("Sigil — a hardened Agent OS.\nKernel as the source of truth. Agents as ephemeral workers.", {
    x: 1, y: 4.7, w: 11, h: 1, fontFace: F.body, fontSize: 18, color: C.accent,
  });

  s.addText("Blueprint · .bob/docs/sigil-architecture-blueprint.md", {
    x: 1, y: H - 0.7, w: 11, h: 0.3, fontFace: F.body, fontSize: 11, color: C.accent, charSpacing: 3,
  });
  pageNum(s, next(), TOTAL, true);
}

// ---- Write ----
const out = "sigil-architecture.pptx";
pres.writeFile({ fileName: out }).then((f) => {
  console.log("Wrote: " + f);
});
