(() => {
  "use strict";

  const REPO = "MesilimLLC/Trackdub2";

  const SAMPLE = [
    {
      delivery: "a4f1c2e0-7b31", at: "2 min ago", event: "pull_request", action: "opened",
      title: "#48 Add Spanish dub export path", url: `https://github.com/${REPO}/pull/48`, author: "mesilim-dev",
      outcome: "commented", tokens: { input: 6120, output: 412 },
      context: "diff --git a/src/Export/DubExportPlanner.cs\n+    if (target.Language == \"es\")\n+        plan.AddTrack(\"es-ES\", mix);\n... 214 lines, 6 files",
      comment: [
        "<p><strong>Summary.</strong> Adds an <code>es-ES</code> track to the export plan.</p>",
        "<ul><li>No test covers the new branch in <code>DubExportPlanner</code>.</li>",
        "<li>The language check is case-sensitive; <code>ES</code> would be skipped.</li></ul>",
      ].join(""),
      steps: [
        ["Signature", "X-Hub-Signature-256 matched", "pass", 1],
        ["Allowlist", REPO, "pass", 0],
        ["Filters", "Not a draft, human author", "pass", 0],
        ["Fetch diff", "214 lines from 6 files", "pass", 380],
        ["Agent call", "gpt-6-luna, no tools", "pass", 4210],
        ["Comment", "Created comment", "pass", 290],
      ],
    },
    {
      delivery: "9c02e8b1-14aa", at: "18 min ago", event: "workflow_run", action: "completed",
      title: "ci / build-and-test failed on feat/console-ui", url: `https://github.com/${REPO}/actions`, author: "github-actions",
      outcome: "edited", tokens: { input: 3880, output: 296 },
      context: "job: test (ubuntu-latest)\n  Failed Trackdub2.Triage.Tests.TriageAgentTests.StartsWithoutEndpoint\n  Expected: 200  Actual: 500\n... last 80 log lines",
      comment: [
        "<p><strong>Failure.</strong> One test in <code>TriageAgentTests</code> returns 500 where 200 is expected.</p>",
        "<ul><li>Likely cause: the Foundry client is created at startup.</li>",
        "<li>Updated the earlier comment on this run instead of posting a new one.</li></ul>",
      ].join(""),
      steps: [
        ["Signature", "X-Hub-Signature-256 matched", "pass", 1],
        ["Allowlist", REPO, "pass", 0],
        ["Fetch logs", "Tail of 1 failed job", "pass", 610],
        ["Agent call", "gpt-6-luna, no tools", "pass", 3120],
        ["Comment", "Edited existing comment", "pass", 240],
      ],
    },
    {
      delivery: "51be77d3-0c9f", at: "41 min ago", event: "workflow_run", action: "completed",
      title: "ci / lint failed on main", url: `https://github.com/${REPO}/actions`, author: "github-actions",
      outcome: "notevaluated", reason: "Hourly agent budget reached (30/30). Not a pass: review this run by hand.",
      context: "job: lint\n  error: 3 formatting issues in src/Trackdub2.Triage/Program.cs",
      steps: [
        ["Signature", "X-Hub-Signature-256 matched", "pass", 1],
        ["Allowlist", REPO, "pass", 0],
        ["Fetch logs", "Tail of 1 failed job", "pass", 520],
        ["Agent call", "Budget reached, call not made", "notevaluated", 0],
      ],
    },
    {
      delivery: "e7d4a019-3b62", at: "1 h ago", event: "pull_request", action: "synchronize",
      title: "#46 Bump Azure.AI.Projects from 1.0.0 to 1.1.0", url: `https://github.com/${REPO}/pull/46`, author: "dependabot[bot]",
      outcome: "skipped", reason: "Bot author",
      steps: [
        ["Signature", "X-Hub-Signature-256 matched", "pass", 1],
        ["Allowlist", REPO, "pass", 0],
        ["Filters", "Author is a bot", "skip", 0],
      ],
    },
    {
      delivery: "2f8c6d55-a7e0", at: "2 h ago", event: "pull_request", action: "opened",
      title: "#45 WIP: audition lane spike", url: `https://github.com/${REPO}/pull/45`, author: "mesilim-dev",
      outcome: "skipped", reason: "Draft pull request",
      steps: [
        ["Signature", "X-Hub-Signature-256 matched", "pass", 1],
        ["Allowlist", REPO, "pass", 0],
        ["Filters", "Pull request is a draft", "skip", 0],
      ],
    },
    {
      delivery: "0b3a9f72-d811", at: "3 h ago", event: "pull_request", action: "opened",
      title: "Delivery with a bad signature", url: null, author: "unknown",
      outcome: "rejected", reason: "Signature mismatch. Returned 401, nothing queued.",
      steps: [["Signature", "X-Hub-Signature-256 did not match", "fail", 1]],
    },
  ];

  const OUTCOME = {
    commented: { label: "Commented", cls: "verdict-pass", group: "commented" },
    edited: { label: "Edited", cls: "verdict-pass", group: "commented" },
    skipped: { label: "Skipped", cls: "verdict-idle", group: "skipped" },
    notevaluated: { label: "Not evaluated", cls: "verdict-notevaluated", group: "attention" },
    rejected: { label: "Rejected", cls: "verdict-fail", group: "attention" },
  };
  const STEP = {
    pass: ["Pass", "verdict-pass"], skip: ["Skipped", "verdict-idle"],
    fail: ["Fail", "verdict-fail"], notevaluated: ["Not evaluated", "verdict-notevaluated"],
  };

  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  const state = { events: SAMPLE.slice(), filter: "all", expanded: new Set([SAMPLE[0].delivery]), configured: null };

  function toast(message) {
    const el = $("toast");
    el.textContent = message;
    el.hidden = false;
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => { el.hidden = true; }, 3200);
  }

  function setPill(id, text, tone) {
    const pill = $(id);
    pill.classList.remove("pill-ok", "pill-warn", "pill-fail");
    pill.classList.add(`pill-${tone}`);
    $(`${id}-value`).textContent = text;
  }

  function renderBanners() {
    const host = $("banners");
    if (state.configured === false) {
      host.innerHTML = `<div class="banner-wrap-warn"><div class="banner">GitHub App not configured. Set <code>Triage__GitHubAppId</code> and <code>Triage__GitHubPrivateKey</code>, then restart. Webhooks return 503 until then. The console still loads.</div></div>`;
    } else if (state.configured === null && state.healthError) {
      host.innerHTML = `<div class="banner-wrap-error"><div class="banner">Could not reach <code>/healthz</code>: ${esc(state.healthError)}</div></div>`;
    } else {
      host.innerHTML = "";
    }
  }

  async function loadHealth() {
    try {
      const res = await fetch("/healthz", { cache: "no-store" });
      const body = await res.json();
      state.configured = Boolean(body.githubConfigured);
      state.healthError = null;
      setPill("pill-health", body.status ?? "unknown", body.status === "ok" ? "ok" : "warn");
      setPill("pill-github", state.configured ? "configured" : "not set", state.configured ? "ok" : "warn");
      $("report-json").textContent = JSON.stringify(body, null, 2);
    } catch (err) {
      state.configured = null;
      state.healthError = err.message;
      setPill("pill-health", "unreachable", "fail");
      setPill("pill-github", "unknown", "warn");
      $("report-json").textContent = `// ${err.message}`;
    }
    renderBanners();
    updateStageButtons();
  }

  function updateStageButtons() {
    document.querySelectorAll("[data-stage]").forEach((btn) => {
      const needsGitHub = btn.dataset.stage !== "ping";
      btn.disabled = needsGitHub && state.configured !== true;
      btn.title = btn.disabled ? "Needs the GitHub App to be configured" : "";
    });
  }

  function renderSummary() {
    const ev = state.events;
    const count = (g) => ev.filter((e) => OUTCOME[e.outcome].group === g).length;
    const budget = Number($("set-budget").value) || 30;
    const calls = ev.filter((e) => e.tokens).length + (ev.some((e) => e.outcome === "notevaluated") ? budget - 2 : 0);
    $("stat-total").textContent = ev.length;
    $("stat-commented").textContent = count("commented");
    $("stat-skipped").textContent = count("skipped");
    $("stat-budget").textContent = `${Math.min(calls, budget)}/${budget}`;

    document.querySelectorAll(".seg-btn").forEach((btn) => {
      const f = btn.dataset.filter;
      const n = f === "all" ? ev.length : count(f);
      btn.innerHTML = `${esc(btn.dataset.label ?? (btn.dataset.label = btn.textContent))}<span class="seg-count">${n}</span>`;
    });

    const tokens = ev.reduce((sum, e) => sum + (e.tokens ? e.tokens.input + e.tokens.output : 0), 0);
    $("settings-estimate").textContent = `${tokens.toLocaleString()} tokens across ${ev.filter((e) => e.tokens).length} agent calls`;
  }

  function stepsPanel(e) {
    const rows = e.steps.map(([stage, detail, result, ms], i) => {
      const [label, cls] = STEP[result];
      return `<div class="attempt-row"><span class="attempt-step">${i + 1}</span><span>${esc(stage)}</span><span>${esc(detail)}</span><span><span class="verdict ${cls}">${label}</span></span><span class="attempt-ms">${ms ? `${ms} ms` : "—"}</span></div>`;
    }).join("");
    const io = e.context
      ? `<div class="director-grid">
          <div><div class="director-label">Untrusted input</div><pre class="code-block">${esc(e.context)}</pre></div>
          <div><div class="director-label">Posted comment</div>${e.comment ? `<div class="comment-body">${e.comment}</div>` : `<div class="comment-body"><p class="line-translation is-empty">No comment posted.</p></div>`}</div>
        </div>`
      : "";
    const usage = e.tokens ? `<span class="token-usage">Tokens: ${e.tokens.input.toLocaleString()} in · ${e.tokens.output.toLocaleString()} out</span>` : "";
    return `<div class="attempts-panel">
      <div class="attempt-row attempt-row-header"><span>#</span><span>Stage</span><span>Detail</span><span>Result</span><span class="attempt-ms">Time</span></div>
      ${rows}${io}${usage}
    </div>`;
  }

  function card(e) {
    const o = OUTCOME[e.outcome];
    const open = state.expanded.has(e.delivery);
    const tone = e.outcome === "rejected" ? " is-failed" : o.group === "attention" ? " is-attention" : "";
    const preview = e.comment
      ? `<div class="line-translation">${esc(e.comment.replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim())}</div>`
      : `<div class="line-translation is-empty">${esc(e.reason ?? "No comment")}</div>`;
    const title = e.url ? `<a href="${esc(e.url)}" target="_blank" rel="noreferrer">${esc(e.title)}</a>` : esc(e.title);
    const reason = e.outcome === "notevaluated" ? `<div class="meaning-issues">${esc(e.reason)}</div>` : "";
    return `<article class="line-card${tone}" role="listitem">
      <div class="line-main">
        <div class="line-top"><span class="line-id">${esc(e.delivery)}</span><span class="line-time">${esc(e.at)}</span></div>
        <div class="line-source">${title}</div>
        ${preview}
        <div class="line-meta">
          <span class="verdict ${o.cls}">${o.label}</span>
          <span class="meta-pill meta-mono">${esc(e.event)}.${esc(e.action)}</span>
          <span class="meta-pill">${esc(e.author)}</span>
          <span>Steps: ${e.steps.length}</span>
          ${e.tokens ? `<span>Tokens: ${(e.tokens.input + e.tokens.output).toLocaleString()}</span>` : ""}
        </div>
      </div>
      ${reason}
      <div class="line-actions">
        <button type="button" class="expand-btn" data-toggle="${esc(e.delivery)}" aria-expanded="${open}">${open ? "Hide steps" : "Show steps"}</button>
        ${o.group === "attention" ? `<button type="button" class="expand-btn" data-retry="${esc(e.delivery)}">Retry delivery</button>` : ""}
      </div>
      ${open ? stepsPanel(e) : ""}
    </article>`;
  }

  function renderLines() {
    const list = state.events.filter((e) => state.filter === "all" || OUTCOME[e.outcome].group === state.filter);
    $("lines").innerHTML = list.map(card).join("");
    $("empty-state").hidden = list.length > 0;
  }

  function render() {
    renderSummary();
    renderLines();
  }

  document.addEventListener("click", (ev) => {
    const target = ev.target.closest("button");
    if (!target) return;

    if (target.dataset.toggle) {
      const id = target.dataset.toggle;
      state.expanded.has(id) ? state.expanded.delete(id) : state.expanded.add(id);
      renderLines();
    } else if (target.dataset.retry) {
      toast(state.configured ? `Queued ${target.dataset.retry} for another run.` : "Retry needs the GitHub App to be configured.");
    } else if (target.dataset.filter) {
      state.filter = target.dataset.filter;
      document.querySelectorAll(".seg-btn").forEach((b) => {
        const active = b === target;
        b.classList.toggle("is-active", active);
        b.setAttribute("aria-pressed", String(active));
      });
      renderLines();
    } else if (target.dataset.stage === "ping") {
      fetch("/github/webhook", { method: "POST", headers: { "X-GitHub-Event": "ping" }, body: "{}" })
        .then((r) => toast(`Webhook answered ${r.status}${r.status === 503 ? " (GitHub App not configured)" : ""}.`))
        .catch((err) => toast(`Webhook unreachable: ${err.message}`));
    } else if (target.dataset.stage) {
      toast("Replays are not wired up yet.");
    } else if (target.id === "btn-sample") {
      state.events = SAMPLE.slice();
      state.expanded = new Set([SAMPLE[0].delivery]);
      render();
      toast("Loaded sample deliveries.");
    }
  });

  $("set-budget").addEventListener("input", renderSummary);

  render();
  loadHealth();
  setInterval(loadHealth, 15000);
})();
