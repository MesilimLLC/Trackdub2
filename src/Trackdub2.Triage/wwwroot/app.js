(() => {
  "use strict";

  const REPO = "MesilimLLC/Trackdub2";

  // Every delivery runs the same pipeline; steps a delivery never reached render as pending.
  const PIPELINE = ["Signature", "Allowlist", "Filters", "Fetch context", "Agent call", "Comment"];

  const SAMPLE = [
    {
      id: "a4f1c2e0-7b31", at: "2 min ago", event: "pull_request", action: "opened",
      title: "#48 Add Spanish dub export path", url: `https://github.com/${REPO}/pull/48`, author: "mesilim-dev",
      outcome: "commented", tokens: { input: 6120, output: 412 },
      inputLabel: "Diff, 214 lines from 6 files",
      input: "diff --git a/src/Export/DubExportPlanner.cs\n+    if (target.Language == \"es\")\n+        plan.AddTrack(\"es-ES\", mix);\n…",
      comment: "<p><strong>Summary.</strong> Adds an <code>es-ES</code> track to the export plan.</p><ul><li>No test covers the new branch in <code>DubExportPlanner</code>.</li><li>The language check is case-sensitive, so <code>ES</code> would be skipped.</li></ul>",
      steps: {
        Signature: ["pass", "X-Hub-Signature-256 matched", 1],
        Allowlist: ["pass", REPO, 0],
        Filters: ["pass", "Not a draft, human author", 0],
        "Fetch context": ["pass", "214 lines from 6 files", 380],
        "Agent call": ["pass", "gpt-6-luna, no tools", 4210],
        Comment: ["pass", "Created comment", 290],
      },
    },
    {
      id: "9c02e8b1-14aa", at: "18 min ago", event: "workflow_run", action: "completed",
      title: "ci / build-and-test failed on feat/console-ui", url: `https://github.com/${REPO}/actions`, author: "github-actions",
      outcome: "edited", tokens: { input: 3880, output: 296 },
      inputLabel: "Log tail, 1 failed job",
      input: "job: test (ubuntu-latest)\n  Failed Trackdub2.Triage.Tests.TriageAgentTests.StartsWithoutEndpoint\n  Expected: 200  Actual: 500\n…",
      comment: "<p><strong>Failure.</strong> One test in <code>TriageAgentTests</code> returns 500 where 200 is expected.</p><ul><li>Likely cause: the Foundry client is created at startup.</li></ul>",
      steps: {
        Signature: ["pass", "X-Hub-Signature-256 matched", 1],
        Allowlist: ["pass", REPO, 0],
        Filters: ["pass", "Failed run on allowed branch", 0],
        "Fetch context": ["pass", "Tail of 1 failed job", 610],
        "Agent call": ["pass", "gpt-6-luna, no tools", 3120],
        Comment: ["pass", "Edited the earlier comment on this run", 240],
      },
    },
    {
      id: "51be77d3-0c9f", at: "41 min ago", event: "workflow_run", action: "completed",
      title: "ci / lint failed on main", url: `https://github.com/${REPO}/actions`, author: "github-actions",
      outcome: "notevaluated", reason: "The hourly agent budget was used up (30 of 30), so no comment was posted. This is not a pass.",
      inputLabel: "Log tail, 1 failed job",
      input: "job: lint\n  error: 3 formatting issues in src/Trackdub2.Triage/Program.cs",
      steps: {
        Signature: ["pass", "X-Hub-Signature-256 matched", 1],
        Allowlist: ["pass", REPO, 0],
        Filters: ["pass", "Failed run on allowed branch", 0],
        "Fetch context": ["pass", "Tail of 1 failed job", 520],
        "Agent call": ["notevaluated", "Budget reached, call not made", 0],
      },
    },
    {
      id: "0b3a9f72-d811", at: "3 h ago", event: "pull_request", action: "opened",
      title: "Delivery with a bad signature", url: null, author: "unknown",
      outcome: "rejected", reason: "The signature did not match the webhook secret. The service returned 401 and queued nothing. Check that the secret in GitHub matches Triage__WebhookSecret.",
      steps: { Signature: ["fail", "X-Hub-Signature-256 did not match", 1] },
    },
    {
      id: "e7d4a019-3b62", at: "1 h ago", event: "pull_request", action: "synchronize",
      title: "#46 Bump Azure.AI.Projects from 1.0.0 to 1.1.0", url: `https://github.com/${REPO}/pull/46`, author: "dependabot[bot]",
      outcome: "skipped", reason: "Skipped because the author is a bot. Change this in Settings.",
      steps: {
        Signature: ["pass", "X-Hub-Signature-256 matched", 1],
        Allowlist: ["pass", REPO, 0],
        Filters: ["skip", "Author is a bot", 0],
      },
    },
    {
      id: "2f8c6d55-a7e0", at: "2 h ago", event: "pull_request", action: "opened",
      title: "#45 WIP: audition lane spike", url: `https://github.com/${REPO}/pull/45`, author: "mesilim-dev",
      outcome: "skipped", reason: "Skipped because the pull request is a draft. It is triaged when marked ready.",
      steps: {
        Signature: ["pass", "X-Hub-Signature-256 matched", 1],
        Allowlist: ["pass", REPO, 0],
        Filters: ["skip", "Pull request is a draft", 0],
      },
    },
  ];

  const OUTCOME = {
    commented: { label: "Commented", tone: "ok", group: "commented" },
    edited: { label: "Comment updated", tone: "ok", group: "commented" },
    notevaluated: { label: "Not evaluated", tone: "warn", group: "attention" },
    rejected: { label: "Rejected", tone: "fail", group: "attention" },
    skipped: { label: "Skipped", tone: "idle", group: "skipped" },
  };
  const STEP_MARK = { pass: "✓", skip: "–", fail: "!", notevaluated: "?" };

  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  const state = {
    events: SAMPLE,
    filter: "all",
    query: "",
    selected: SAMPLE.find((e) => OUTCOME[e.outcome].group === "attention")?.id ?? SAMPLE[0].id,
    configured: null,
    reachable: null,
    pingOk: false,
  };

  function toast(message) {
    const el = $("toast");
    el.textContent = message;
    el.hidden = false;
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => { el.hidden = true; }, 3200);
  }

  function setChip(id, text, tone) {
    const chip = $(id);
    chip.classList.remove("tone-ok", "tone-warn", "tone-fail");
    chip.classList.add(`tone-${tone}`);
    $(`${id}-value`).textContent = text;
  }

  function visibleEvents() {
    const q = state.query.trim().toLowerCase();
    return state.events.filter((e) =>
      (state.filter === "all" || OUTCOME[e.outcome].group === state.filter) &&
      (!q || `${e.title} ${e.author} ${e.event}.${e.action} ${e.id}`.toLowerCase().includes(q)));
  }

  function renderBudget() {
    const budget = Math.max(1, Number($("set-budget").value) || 30);
    const capped = state.events.some((e) => e.outcome === "notevaluated");
    const used = capped ? budget : Math.min(budget, state.events.filter((e) => e.tokens).length);
    $("budget-text").textContent = `${used} / ${budget}`;
    const fill = $("budget-fill");
    fill.style.width = `${(used / budget) * 100}%`;
    fill.classList.toggle("is-full", used >= budget);
  }

  function renderSetup() {
    const done = { service: state.reachable === true, github: state.configured === true, ping: state.pingOk };
    const order = ["service", "github", "ping"];
    const current = order.find((k) => !done[k]);
    document.querySelectorAll(".setup-step").forEach((li) => {
      const key = li.dataset.step;
      li.classList.toggle("is-done", done[key]);
      li.classList.toggle("is-current", key === current);
    });
    $("setup-progress").textContent = `${order.filter((k) => done[k]).length} of 3 done`;
    $("setup").hidden = !current;
  }

  function renderTabs() {
    document.querySelectorAll(".tab").forEach((tab) => {
      const f = tab.dataset.filter;
      const n = f === "all" ? state.events.length : state.events.filter((e) => OUTCOME[e.outcome].group === f).length;
      const count = tab.querySelector(".count");
      count.textContent = n;
      count.classList.toggle("has-items", n > 0);
      const active = f === state.filter;
      tab.classList.toggle("is-active", active);
      tab.setAttribute("aria-selected", String(active));
    });
  }

  function renderInbox() {
    const list = visibleEvents();
    if (list.length && !list.some((e) => e.id === state.selected)) state.selected = list[0].id;
    $("inbox-list").innerHTML = list.map((e) => {
      const o = OUTCOME[e.outcome];
      const selected = e.id === state.selected;
      return `<li class="inbox-item tone-${o.tone}${selected ? " is-selected" : ""}" data-id="${esc(e.id)}" role="option" aria-selected="${selected}">
        <span class="dot" aria-hidden="true"></span>
        <div class="item-body">
          <div class="item-top"><span class="item-title">${esc(e.title)}</span><span class="item-time">${esc(e.at)}</span></div>
          <div class="item-meta"><span class="item-outcome out-${o.tone}">${o.label}</span><span class="mono">${esc(e.event)}</span><span>${esc(e.author)}</span></div>
        </div>
      </li>`;
    }).join("");
    $("inbox-list").setAttribute("role", "listbox");
    $("inbox-empty").hidden = list.length > 0;
  }

  function pipeline(e) {
    const halted = Object.values(e.steps).some(([r]) => r !== "pass");
    return PIPELINE.map((name, i) => {
      const step = e.steps[name];
      const [result, detail, ms] = step ?? ["pending", halted ? "Not reached" : "Waiting", 0];
      const mark = STEP_MARK[result] ?? i + 1;
      return `<li class="stage is-${result}">
        <span class="stage-mark" aria-hidden="true">${mark}</span>
        <div class="stage-name">${esc(name)}<span class="sr-only">: ${result}</span></div>
        <div class="stage-detail">${esc(detail)}</div>
        ${ms ? `<div class="stage-ms">${ms.toLocaleString()} ms</div>` : ""}
      </li>`;
    }).join("");
  }

  function renderDetail() {
    const e = state.events.find((x) => x.id === state.selected);
    const host = $("detail");
    if (!e || !visibleEvents().includes(e)) {
      host.innerHTML = `<div class="card"><div class="comment-empty">Select a delivery to see what happened.</div></div>`;
      return;
    }
    const o = OUTCOME[e.outcome];
    const total = Object.values(e.steps).reduce((s, [, , ms]) => s + ms, 0);
    const title = e.url ? `<a href="${esc(e.url)}" target="_blank" rel="noreferrer">${esc(e.title)}</a>` : esc(e.title);
    const canRetry = o.group === "attention";

    const callout = e.reason
      ? `<div class="callout callout-${o.tone === "ok" ? "idle" : o.tone}" role="note">
          <span>${esc(e.reason)}</span>
          ${canRetry ? `<button type="button" class="btn btn-small" data-action="retry">Retry delivery</button>` : ""}
        </div>`
      : "";

    const io = e.input
      ? `<div class="io">
          <section class="card" aria-label="Agent input">
            <div class="card-head"><h3>Agent input</h3><span class="muted">${esc(e.inputLabel)} · untrusted</span></div>
            <pre class="code">${esc(e.input)}</pre>
          </section>
          <section class="card" aria-label="Posted comment">
            <div class="card-head"><h3>Posted comment</h3>${e.url ? `<a href="${esc(e.url)}" target="_blank" rel="noreferrer" class="muted">View on GitHub</a>` : ""}</div>
            ${e.comment
              ? `<div class="comment"><span class="comment-avatar" aria-hidden="true">T2</span><div class="comment-content"><div class="comment-author">trackdub2-triage <span>bot · ${esc(e.at)}</span></div><div class="comment-text">${e.comment}</div></div></div>`
              : `<div class="comment-empty">No comment was posted.</div>`}
          </section>
        </div>`
      : "";

    host.innerHTML = `
      <div class="detail-head">
        <div>
          <div class="detail-kicker"><span class="badge out-${o.tone}">${o.label}</span><span class="tag">${esc(e.event)}.${esc(e.action)}</span><span>${esc(e.author)}</span><span>·</span><span>${esc(e.at)}</span></div>
          <h2 class="detail-title">${title}</h2>
        </div>
        <div class="detail-actions">
          ${o.group === "commented" ? `<button type="button" class="btn" data-action="rerun">Re-run triage</button>` : ""}
          <button type="button" class="btn btn-ghost" data-action="copy-id">Copy delivery ID</button>
        </div>
      </div>
      ${callout}
      <section class="card" aria-labelledby="pipeline-heading">
        <div class="card-head"><h3 id="pipeline-heading">Pipeline</h3><span class="muted">${total.toLocaleString()} ms total</span></div>
        <ol class="pipeline">${pipeline(e)}</ol>
      </section>
      ${io}
      <section class="card" aria-label="Delivery facts">
        <dl class="facts">
          <div><dt>Delivery</dt><dd>${esc(e.id)}</dd></div>
          <div><dt>Repository</dt><dd>${REPO}</dd></div>
          ${e.tokens ? `<div><dt>Tokens in</dt><dd>${e.tokens.input.toLocaleString()}</dd></div><div><dt>Tokens out</dt><dd>${e.tokens.output.toLocaleString()}</dd></div>` : ""}
        </dl>
      </section>`;

    host.querySelectorAll("[data-action='retry'], [data-action='rerun']").forEach((b) => {
      b.disabled = state.configured !== true;
      if (b.disabled) b.title = "Needs the GitHub App credentials";
    });
  }

  function render() {
    renderTabs();
    renderInbox();
    renderDetail();
    renderBudget();
  }

  function select(id, focusDetail) {
    state.selected = id;
    renderInbox();
    renderDetail();
    $("inbox-list").querySelector(".is-selected")?.scrollIntoView({ block: "nearest" });
    if (focusDetail && window.matchMedia("(max-width: 900px)").matches) $("detail").focus();
  }

  function move(delta) {
    const list = visibleEvents();
    if (!list.length) return;
    const i = list.findIndex((e) => e.id === state.selected);
    select(list[Math.min(list.length - 1, Math.max(0, i + delta))].id);
  }

  async function loadHealth() {
    try {
      const res = await fetch("/healthz", { cache: "no-store" });
      const body = await res.json();
      state.reachable = res.ok;
      state.configured = Boolean(body.githubConfigured);
      setChip("chip-health", body.status === "ok" ? "Healthy" : String(body.status ?? "Unknown"), body.status === "ok" ? "ok" : "warn");
      setChip("chip-github", state.configured ? "Connected" : "Not configured", state.configured ? "ok" : "warn");
      $("health-json").textContent = JSON.stringify(body, null, 2);
    } catch (err) {
      state.reachable = false;
      state.configured = null;
      setChip("chip-health", "Unreachable", "fail");
      setChip("chip-github", "Unknown", "warn");
      $("health-json").textContent = `// ${err.message}`;
    }
    renderSetup();
    renderDetail();
  }

  async function ping(button) {
    button.disabled = true;
    try {
      const res = await fetch("/github/webhook", { method: "POST", headers: { "X-GitHub-Event": "ping", "Content-Type": "application/json" }, body: "{}" });
      state.pingOk = res.ok;
      if (res.ok) toast("Webhook answered 200. GitHub deliveries will work.");
      else if (res.status === 503) toast("Webhook answered 503: add the GitHub App credentials first.");
      else if (res.status === 401) toast("Webhook answered 401: the ping was not signed. Send one from GitHub's App settings.");
      else toast(`Webhook answered ${res.status}.`);
    } catch (err) {
      toast(`Webhook unreachable: ${err.message}`);
    } finally {
      button.disabled = false;
      renderSetup();
    }
  }

  document.addEventListener("click", (ev) => {
    const item = ev.target.closest(".inbox-item");
    if (item) return select(item.dataset.id, true);

    const btn = ev.target.closest("button");
    if (!btn) return;
    if (btn.dataset.filter) {
      state.filter = btn.dataset.filter;
      render();
    } else if (btn.dataset.action === "ping") {
      ping(btn);
    } else if (btn.dataset.action === "retry" || btn.dataset.action === "rerun") {
      toast(`Queued ${state.selected} for another run.`);
    } else if (btn.dataset.action === "copy-id") {
      navigator.clipboard?.writeText(state.selected).then(() => toast("Delivery ID copied."), () => toast(state.selected));
    } else if (btn.dataset.copy) {
      navigator.clipboard?.writeText(btn.dataset.copy).then(() => toast("Variable names copied."), () => toast("Copy failed."));
    } else if (btn.id === "open-settings") {
      $("settings").showModal();
    }
  });

  $("settings").addEventListener("click", (ev) => {
    if (ev.target === ev.currentTarget) ev.currentTarget.close();
  });

  $("search").addEventListener("input", (ev) => {
    state.query = ev.target.value;
    renderInbox();
    renderDetail();
  });

  $("set-budget").addEventListener("input", renderBudget);
  $("set-model").addEventListener("change", (ev) => { $("chip-model").textContent = ev.target.value; });

  document.addEventListener("keydown", (ev) => {
    if (ev.metaKey || ev.ctrlKey || ev.altKey || $("settings").open) return;
    const typing = ev.target.matches("input, select, textarea");
    if (ev.key === "/" && !typing) {
      ev.preventDefault();
      $("search").focus();
    } else if (ev.key === "Escape" && ev.target.id === "search") {
      ev.target.blur();
    } else if (!typing && (ev.key === "j" || ev.key === "ArrowDown")) {
      ev.preventDefault();
      move(1);
    } else if (!typing && (ev.key === "k" || ev.key === "ArrowUp")) {
      ev.preventDefault();
      move(-1);
    }
  });

  render();
  loadHealth();
  setInterval(loadHealth, 15000);
})();
