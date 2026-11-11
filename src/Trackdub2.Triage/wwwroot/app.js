"use strict";

const WEBHOOK_URL = "https://app-td2-triage-42dkfedvehtze.azurewebsites.net/github/webhook";

const ICONS = {
  pr: '<svg viewBox="0 0 24 24"><circle cx="6" cy="6" r="2.5"/><circle cx="6" cy="18" r="2.5"/><circle cx="18" cy="18" r="2.5"/><path d="M6 8.5v7M18 15.5V10a3 3 0 0 0-3-3h-4"/><path d="m13 5-2 2 2 2"/></svg>',
  ci: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m5 12 5 5 9-10"/></svg>',
  arrow: '<svg viewBox="0 0 24 24"><path d="M5 12h14M13 6l6 6-6 6"/></svg>',
};

// Placeholder data until the service exposes triage history.
const TRIAGE = [
  { id: 1, type: "pr", num: 48, title: "Port timing gate from Director with tolerance table", author: "amora", verdict: "ok", verdictText: "Looks good", tokens: 6120, mins: 4,
    summary: "Ports the timing gate and its tolerance table. Behavior matches Director for the 14 fixtures included.",
    points: ["`TimingGate.Evaluate` rounds before comparing; Director compared raw milliseconds. Confirm this is intended.", "Fixtures cover only single-speaker clips. Consider one overlapping-dialogue case."] },
  { id: 2, type: "ci", num: 1182, title: "ci · tests (ubuntu-latest) failed on main", author: "github-actions", verdict: "bad", verdictText: "Failing", tokens: 3340, mins: 11,
    summary: "`HourlyCallBudgetTests.ResetsAfterWindow` fails because the fake clock advances 59m instead of 60m.",
    log: "  Failed HourlyCallBudgetTests.ResetsAfterWindow [12 ms]\n  Error Message:\n   <span class=\"err\">Assert.True() Failure</span>\n   Expected: True\n   Actual:   False",
    points: ["Advance the `FakeTimeProvider` by `TimeSpan.FromHours(1)` in the test.", "No production code change needed."] },
  { id: 3, type: "pr", num: 47, title: "Add Speech synthesis client with SSML prosody hints", author: "jdelgado", verdict: "warn", verdictText: "Needs changes", tokens: 8410, mins: 38,
    summary: "Adds the Speech client. Retries are unbounded on 429, which can exhaust the hourly budget.",
    points: ["Cap retries in `SynthesisClient.SendAsync` and honor `Retry-After`.", "SSML is built by string concatenation; escape line text before inserting it."] },
  { id: 4, type: "pr", num: 46, title: "Bicep: add Key Vault reference for webhook secret", author: "amora", verdict: "ok", verdictText: "Looks good", tokens: 2950, mins: 125,
    summary: "Wires the webhook secret through a Key Vault reference. Role assignment scope is correct.", points: ["No issues found."] },
  { id: 5, type: "ci", num: 1176, title: "ci · bicep build failed on fix/storage-sku", author: "github-actions", verdict: "bad", verdictText: "Failing", tokens: 1880, mins: 210,
    summary: "`Standard_ZRS` isn't available for the chosen storage kind in eastus.",
    log: "  main.bicep(42,5) : <span class=\"err\">Error BCP036</span>: The property \"sku\" expected a value of type ...",
    points: ["Use `Standard_LRS` for the spike, or switch kind to `StorageV2`."] },
  { id: 6, type: "pr", num: 45, title: "Meaning gate: compare back-translation embeddings", author: "rkhan", verdict: "warn", verdictText: "Needs changes", tokens: 7230, mins: 380,
    summary: "Threshold of 0.82 is hardcoded in two places and disagrees with the grader (0.85).",
    points: ["Move the threshold to `GateOptions` and share it with `grader/`.", "Add a test where idioms translate non-literally."] },
];

const RUNS = [
  { title: "Episode 04 · cold open", file: "ep04_open.wav", dur: "02:14", lines: 38, status: "ok", statusText: "Passed", gates: [["Timing", "ok"], ["Meaning", "ok"], ["Audio", "ok"]], cost: "$0.92", live: false },
  { title: "Trailer v3", file: "trailer_v3.wav", dur: "01:02", lines: 17, status: "warn", statusText: "Running", gates: [["Timing", "ok"], ["Meaning", "warn"], ["Audio", "muted"]], cost: "$0.31", live: true },
  { title: "Interview · Dr. Ruiz", file: "ruiz_int.wav", dur: "06:48", lines: 112, status: "bad", statusText: "2 lines blocked", gates: [["Timing", "bad"], ["Meaning", "ok"], ["Audio", "ok"]], cost: "$2.77", live: false },
];

const STAGES = [
  ["Input", "Transcribe", "Azure Speech STT"],
  ["Agent", "Plan lines", "Orchestrator"],
  ["Agent", "Translate", "EN → ES"],
  ["Gate", "Timing", "Fits source slot"],
  ["Gate", "Meaning", "Back-translation"],
  ["Agent", "Synthesize", "Neural voice"],
  ["Gate", "Audio", "Measured length"],
];

const state = { health: null, filter: "all", query: "", lastFocus: null };

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];
const esc = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
const inline = (s) => esc(s).replace(/`([^`]+)`/g, "<code>$1</code>");
const ago = (m) => (m < 60 ? `${m}m` : m < 1440 ? `${Math.round(m / 60)}h` : `${Math.round(m / 1440)}d`);
const pillClass = (v) => ({ ok: "pill-ok", warn: "pill-warn", bad: "pill-bad" })[v] || "pill-muted";

function toast(msg) {
  const t = $("#toast");
  t.textContent = msg;
  t.hidden = false;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => (t.hidden = true), 2200);
}

/* Routing */
const ROUTES = { overview: "Overview", triage: "Triage", dubbing: "Dubbing", setup: "Setup", settings: "Settings" };
function route() {
  const name = location.hash.slice(1) in ROUTES ? location.hash.slice(1) : "overview";
  $$(".view").forEach((v) => (v.hidden = v.dataset.view !== name));
  $$(".nav-link").forEach((a) => (a.dataset.route === name ? a.setAttribute("aria-current", "page") : a.removeAttribute("aria-current")));
  $("#crumb-current").textContent = ROUTES[name];
  document.title = `${ROUTES[name]} · Trackdub 2`;
  closeSidebar();
  window.scrollTo(0, 0);
}

/* Health */
async function checkHealth(announce) {
  try {
    const res = await fetch("/healthz", { cache: "no-store" });
    if (!res.ok) throw new Error(String(res.status));
    state.health = await res.json();
  } catch {
    state.health = { status: "down" };
  }
  renderHealth();
  renderSetup();
  if (announce) toast(state.health.status === "ok" ? "Service is healthy" : "Service unreachable");
}

function renderHealth() {
  const h = state.health;
  const up = h?.status === "ok";
  const configured = !!h?.githubConfigured;
  const cls = !up ? "bad" : configured ? "ok" : "warn";
  const label = !up ? "Service unreachable" : configured ? "Healthy · receiving webhooks" : "Healthy · GitHub not connected";

  const pill = $("#health-pill");
  $(".status-dot", pill).className = `status-dot ${cls}`;
  $(".health-text", pill).textContent = label;
  $("#side-dot").className = `status-dot ${cls}`;
  $("#side-status").textContent = !up ? "Offline" : configured ? "Operational" : "Awaiting GitHub";
  $("#setup-banner").hidden = !up || configured;
  $("#kv-appid").textContent = configured ? "Configured" : "Not set";
  $("#kv-foundry").textContent = up ? "Lazy · on first call" : "—";
}

/* Overview */
function renderRecent() {
  $("#recent-list").innerHTML = TRIAGE.slice(0, 5).map((t) => `
    <li><button data-open="${t.id}">
      <span class="act-icon" aria-hidden="true">${ICONS[t.type]}</span>
      <span class="act-main">
        <span class="act-title">${esc(t.title)}</span>
        <span class="act-meta">${t.type === "pr" ? "PR" : "Run"} #${t.num} · ${esc(t.author)}</span>
      </span>
      <span class="pill ${pillClass(t.verdict)}">${t.verdictText}</span>
      <span class="act-time">${ago(t.mins)}</span>
    </button></li>`).join("");
  $("#nav-triage-count").textContent = TRIAGE.length;
}

function renderPipeline() {
  $("#pipeline-overview").innerHTML = STAGES.map(([kind, name, meta], i) => `
    ${i ? `<span class="connector" aria-hidden="true">${ICONS.arrow}</span>` : ""}
    <div class="stage ${kind === "Gate" ? "gate" : ""}">
      <span class="stage-kind">${kind}</span>
      <span class="stage-name">${name}</span>
      <span class="stage-meta">${meta}</span>
    </div>`).join("");
}

/* Setup */
function setupSteps() {
  const configured = !!state.health?.githubConfigured;
  return [
    { title: "Deploy infrastructure", done: true, text: "Foundry, Speech, storage, Key Vault and App Service from <span class=\"mono\">infra/main.bicep</span>." },
    { title: "Create the GitHub App", done: configured, text: "On MesilimLLC, name it <strong>Trackdub2 Triage</strong>. Point the webhook here and paste the secret from Key Vault.",
      copy: WEBHOOK_URL, perms: ["Pull requests: Read & write", "Actions: Read", "Metadata: Read"] },
    { title: "Store the private key", done: configured, text: "Generate a key, save it to Key Vault as <span class=\"mono\">github-app-private-key</span>, then delete the .pem.",
      copy: "az keyvault secret set --vault-name kv-td2-42dkfedvehtze --name github-app-private-key --file <key>.pem" },
    { title: "Redeploy with the App ID", done: configured, text: "Set <span class=\"mono\">GITHUB_APP_ID</span> so Bicep stays the source of truth, then restart the web app." },
    { title: "Install on Trackdub2", done: configured, text: "Install App › MesilimLLC › Only select repositories › Trackdub2." },
    { title: "Verify health", done: configured, text: "<span class=\"mono\">/healthz</span> should report <span class=\"mono\">githubConfigured: true</span>. Then open a test pull request." },
  ];
}

function renderSetup() {
  const steps = setupSteps();
  const doneCount = steps.filter((s) => s.done).length;
  const currentIdx = steps.findIndex((s) => !s.done);

  $("#progress-count").textContent = `${doneCount} / ${steps.length}`;
  $("#ring-fill").style.strokeDashoffset = String(213.6 * (1 - doneCount / steps.length));
  $("#ring-title").textContent = doneCount === steps.length ? "All set" : doneCount ? "In progress" : "Not started";

  $("#mini-steps").innerHTML = steps.map((s) => `
    <li class="${s.done ? "done" : ""}"><span class="check" aria-hidden="true">${s.done ? ICONS.check : ""}</span>${s.title}<span class="sr-only">${s.done ? " (done)" : " (to do)"}</span></li>`).join("");

  $("#steps").innerHTML = steps.map((s, i) => `
    <li class="step ${s.done ? "done" : i === currentIdx ? "current" : ""}">
      <span class="step-num" aria-hidden="true">${s.done ? ICONS.check.replace("<svg", '<svg width="12" height="12" fill="none" stroke="currentColor" stroke-width="3"') : i + 1}</span>
      <div class="step-body">
        <div class="step-title">${s.title}
          ${s.done ? '<span class="pill pill-ok">Done</span>' : i === currentIdx ? '<span class="pill pill-warn">Next</span>' : ""}
        </div>
        <p class="step-text">${s.text}</p>
        ${s.perms ? `<div class="perm-list">${s.perms.map((p) => `<span class="pill pill-muted">${p}</span>`).join("")}</div>` : ""}
        ${s.copy ? `<div class="copy-row"><code>${esc(s.copy)}</code><button class="copy-btn" data-copy="${esc(s.copy)}">Copy</button></div>` : ""}
      </div>
    </li>`).join("");
}

/* Triage */
function renderTriage() {
  const q = state.query.toLowerCase();
  const rows = TRIAGE.filter((t) => (state.filter === "all" || t.type === state.filter) &&
    (!q || `${t.title} ${t.author} ${t.num}`.toLowerCase().includes(q)));
  $("#triage-body").innerHTML = rows.map((t) => `
    <tr tabindex="0" data-open="${t.id}" aria-label="Open ${esc(t.title)}">
      <td><div class="item-cell"><span>${esc(t.title)}</span><span class="item-num">${t.type === "pr" ? "#" : "run "}${t.num}</span></div></td>
      <td><span class="muted">${t.type === "pr" ? "Pull request" : "CI failure"}</span></td>
      <td><span class="pill ${pillClass(t.verdict)}">${t.verdictText}</span></td>
      <td><span class="avatar"><span aria-hidden="true">${esc(t.author.slice(0, 2).toUpperCase())}</span><span>${esc(t.author)}</span></span></td>
      <td class="num mono">${t.tokens.toLocaleString()}</td>
      <td class="num mono muted">${ago(t.mins)} ago</td>
    </tr>`).join("");
  $("#triage-empty").hidden = rows.length > 0;
}

/* Dubbing */
function renderRuns() {
  $("#runs").innerHTML = RUNS.map((r, ri) => {
    const bars = Array.from({ length: 48 }, (_, i) => {
      const h = 18 + Math.abs(Math.sin(i * 0.55 + ri * 2) * 60 + Math.sin(i * 1.7 + ri) * 22);
      return `<span style="height:${Math.min(100, h).toFixed(0)}%"></span>`;
    }).join("");
    return `
    <article class="run">
      <div class="run-head">
        <div><p class="run-title">${r.title}</p><p class="run-sub">${r.file} · ${r.dur} · ${r.lines} lines</p></div>
        <span class="pill ${pillClass(r.status)}">${r.statusText}</span>
      </div>
      <div class="wave ${r.live ? "live" : ""}" aria-hidden="true">${bars}</div>
      <div class="gates" aria-label="Gate results">${r.gates.map(([g, v]) => `<span class="pill ${pillClass(v)}">${g}</span>`).join("")}</div>
      <div class="run-foot"><span>Cost <span class="mono">${r.cost}</span></span><span class="sample-chip">Sample</span></div>
    </article>`;
  }).join("");
}

/* Drawer */
function openDrawer(id) {
  const t = TRIAGE.find((x) => x.id === Number(id));
  if (!t) return;
  state.lastFocus = document.activeElement;
  $("#drawer-eyebrow").textContent = t.type === "pr" ? `Pull request #${t.num}` : `Workflow run ${t.num}`;
  $("#drawer-title").textContent = t.title;
  $("#drawer-body").innerHTML = `
    <dl class="meta-grid">
      <div><dt>Verdict</dt><dd><span class="pill ${pillClass(t.verdict)}">${t.verdictText}</span></dd></div>
      <div><dt>Tokens</dt><dd class="mono">${t.tokens.toLocaleString()}</dd></div>
      <div><dt>Posted</dt><dd>${ago(t.mins)} ago</dd></div>
    </dl>
    ${t.log ? `<div><h3 class="small muted" style="margin-bottom:8px">Failed job log tail</h3><pre class="log">${t.log}</pre></div>` : ""}
    <div class="comment">
      <div class="comment-head"><strong>trackdub2-triage</strong><span class="bot-badge">bot</span><span>commented ${ago(t.mins)} ago</span></div>
      <div class="comment-body">
        <h3>Summary</h3>
        <p>${inline(t.summary)}</p>
        <h3>Worth a look</h3>
        <ul>${t.points.map((p) => `<li>${inline(p)}</li>`).join("")}</ul>
        <p class="muted small">Written by gpt-6-luna. Edited in place on new pushes.</p>
      </div>
    </div>
    <a class="btn btn-ghost" href="https://github.com/MesilimLLC/Trackdub2/${t.type === "pr" ? `pull/${t.num}` : "actions"}" target="_blank" rel="noopener noreferrer">Open on GitHub</a>`;
  $("#drawer").hidden = false;
  $("#drawer-backdrop").hidden = false;
  $("#drawer-close").focus();
}

function closeDrawer() {
  if ($("#drawer").hidden) return;
  $("#drawer").hidden = true;
  $("#drawer-backdrop").hidden = true;
  state.lastFocus?.focus?.();
}

/* Mobile sidebar */
function closeSidebar() {
  $(".sidebar").classList.remove("open");
  $("#menu-btn").setAttribute("aria-expanded", "false");
}

/* Events */
document.addEventListener("click", async (e) => {
  const open = e.target.closest("[data-open]");
  if (open) return openDrawer(open.dataset.open);

  const copy = e.target.closest("[data-copy]");
  if (copy) {
    try { await navigator.clipboard.writeText(copy.dataset.copy); toast("Copied to clipboard"); }
    catch { toast("Copy failed. Select the text instead."); }
    return;
  }

  const filter = e.target.closest("[data-filter]");
  if (filter) {
    state.filter = filter.dataset.filter;
    $$("[data-filter]").forEach((b) => b.setAttribute("aria-selected", String(b === filter)));
    return renderTriage();
  }
});

document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { closeDrawer(); closeSidebar(); }
  if ((e.key === "Enter" || e.key === " ") && e.target.matches("tr[data-open]")) {
    e.preventDefault();
    openDrawer(e.target.dataset.open);
  }
  const typing = e.target.matches("input, textarea");
  if (e.key === "/" && !typing) {
    e.preventDefault();
    location.hash = "triage";
    requestAnimationFrame(() => $("#triage-search").focus());
  }
});

$("#triage-search").addEventListener("input", (e) => { state.query = e.target.value; renderTriage(); });
$("#cmd-btn").addEventListener("click", () => { location.hash = "triage"; requestAnimationFrame(() => $("#triage-search").focus()); });
$("#drawer-close").addEventListener("click", closeDrawer);
$("#drawer-backdrop").addEventListener("click", closeDrawer);
$("#recheck-btn").addEventListener("click", () => checkHealth(true));
$("#new-run-btn").addEventListener("click", () => toast("Pipeline CLI isn't wired up yet"));
$("#menu-btn").addEventListener("click", () => {
  const open = $(".sidebar").classList.toggle("open");
  $("#menu-btn").setAttribute("aria-expanded", String(open));
});
window.addEventListener("hashchange", route);

renderRecent();
renderPipeline();
renderTriage();
renderRuns();
renderSetup();
route();
checkHealth(false);
setInterval(() => checkHealth(false), 30000);
