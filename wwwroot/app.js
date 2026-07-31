// DetonatorAgent web UI — mirrors detonate.ps1 workflow.
// Flow: acquire lock -> XOR-encrypt file client-side -> upload+execute ->
//       poll EDR alerts live -> kill (if ok) -> fetch final logs -> release lock.

(() => {
    "use strict";

    const $ = (id) => document.getElementById(id);

    const form            = $("detonate-form");
    const submitBtn       = $("submit-btn");
    const progressPanel   = $("progress-panel");
    const statusLine      = $("status-line");
    const uploadFill      = $("upload-fill");
    const uploadPercent   = $("upload-percent");
    const runProgressWrap = $("run-progress-wrap");
    const runFill         = $("run-fill");
    const runElapsed      = $("run-elapsed");
    const liveAlertsBody  = document.querySelector("#live-alerts tbody");
    const liveAlertsEmpty = $("live-alerts-empty");
    const resultsPanel    = $("results-panel");
    const resultStatus    = $("result-status");
    const resultPid       = $("result-pid");
    const resultMessage   = $("result-message");
    const finalAlertsBody = document.querySelector("#final-alerts tbody");
    const finalAlertsEmpty= $("final-alerts-empty");
    const execStdout      = $("exec-stdout");
    const execStderr      = $("exec-stderr");
    const agentLogsEl     = $("agent-logs");

    // Track seen alert IDs across the live poll to avoid duplicates.
    const seenAlerts = new Set();

    function setStatus(text) {
        statusLine.textContent = text;
    }

    function setUploadProgress(pct) {
        const p = Math.max(0, Math.min(100, pct));
        uploadFill.style.width = p + "%";
        uploadPercent.textContent = Math.round(p) + "%";
    }

    function setRunProgress(elapsed, total) {
        const pct = total > 0 ? (elapsed / total) * 100 : 0;
        runFill.style.width = Math.min(100, pct) + "%";
        runElapsed.textContent = `${elapsed} / ${total} s`;
    }

    function resetUI() {
        seenAlerts.clear();
        liveAlertsBody.innerHTML = "";
        liveAlertsEmpty.hidden = false;
        finalAlertsBody.innerHTML = "";
        finalAlertsEmpty.hidden = false;
        resultsPanel.hidden = true;
        resultMessage.hidden = true;
        resultMessage.textContent = "";
        resultStatus.className = "badge";
        resultStatus.textContent = "–";
        resultPid.textContent = "";
        execStdout.textContent = "";
        execStderr.textContent = "";
        agentLogsEl.textContent = "";
        setUploadProgress(0);
        setRunProgress(0, 0);
        runProgressWrap.hidden = true;
        // Remove any restored-from-server banner from a previous session.
        for (const el of resultsPanel.querySelectorAll(".restore-banner")) {
            el.remove();
        }
    }

    function appendAlertRow(tbody, alert) {
        const tr = document.createElement("tr");
        for (const key of ["title", "severity", "category", "alertId"]) {
            const td = document.createElement("td");
            td.textContent = alert[key] != null ? String(alert[key]) : "";
            tr.appendChild(td);
        }
        tbody.appendChild(tr);
    }

    function sleep(ms) {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }

    async function fetchJson(url, opts) {
        const res = await fetch(url, opts);
        const text = await res.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
        return { ok: res.ok, status: res.status, data, text };
    }

    async function acquireLock() {
        const res = await fetchJson("/api/lock/acquire", { method: "POST" });
        if (res.status === 409) {
            throw new Error("Resource is already in use (lock held by another client).");
        }
        if (!res.ok) {
            throw new Error(`Failed to acquire lock (HTTP ${res.status}).`);
        }
    }

    async function releaseLock() {
        try {
            await fetch("/api/lock/release", { method: "POST" });
        } catch (e) {
            console.warn("Failed to release lock:", e);
        }
    }

    // XOR-encrypt the file bytes with a random key 1..255 (matches detonate.ps1).
    async function xorEncryptFile(file) {
        const buf = await file.arrayBuffer();
        const src = new Uint8Array(buf);
        const key = 1 + Math.floor(Math.random() * 255); // 1..255
        const out = new Uint8Array(src.length);
        for (let i = 0; i < src.length; i++) {
            out[i] = src[i] ^ key;
        }
        return { blob: new Blob([out], { type: "application/octet-stream" }), key };
    }

    // Upload the encrypted file with progress reporting via XHR.
    function uploadAndExecute(params) {
        return new Promise((resolve, reject) => {
            const fd = new FormData();
            fd.append("file", params.blob, params.fileName);
            fd.append("drop_path", params.dropPath);
            fd.append("xor_key", String(params.xorKey));
            if (params.executableArgs) fd.append("executable_args", params.executableArgs);
            if (params.executionMode)  fd.append("execution_mode",  params.executionMode);

            const xhr = new XMLHttpRequest();
            xhr.open("POST", "/api/execute/exec");

            xhr.upload.addEventListener("progress", (evt) => {
                if (evt.lengthComputable) {
                    setUploadProgress((evt.loaded / evt.total) * 100);
                }
            });
            xhr.upload.addEventListener("load", () => setUploadProgress(100));

            xhr.addEventListener("load", () => {
                let data = null;
                try { data = JSON.parse(xhr.responseText); } catch { /* ignore */ }
                resolve({ httpStatus: xhr.status, data, raw: xhr.responseText });
            });
            xhr.addEventListener("error", () => reject(new Error("Network error during upload.")));
            xhr.addEventListener("abort", () => reject(new Error("Upload aborted.")));

            xhr.send(fd);
        });
    }

    // Poll EDR alerts every `intervalMs` for `totalSeconds`.
    // Diffs by alertId, appends new rows to the live alerts table.
    async function pollEdrAlerts(totalSeconds, intervalMs = 1000) {
        runProgressWrap.hidden = false;
        setRunProgress(0, totalSeconds);

        for (let elapsed = 0; elapsed < totalSeconds; elapsed++) {
            const res = await fetchJson("/api/logs/edr");
            if (res.ok && res.data && Array.isArray(res.data.alerts)) {
                for (const alert of res.data.alerts) {
                    const id = alert.alertId;
                    if (id != null && !seenAlerts.has(id)) {
                        seenAlerts.add(id);
                        appendAlertRow(liveAlertsBody, alert);
                        liveAlertsEmpty.hidden = true;
                    }
                }
            }
            await sleep(intervalMs);
            setRunProgress(elapsed + 1, totalSeconds);
        }
    }

    async function killProcess() {
        try {
            await fetch("/api/execute/kill", { method: "POST" });
        } catch (e) {
            console.warn("Kill failed:", e);
        }
    }

    // Fetch EDR, execution and agent logs from the server and render them
    // into the results panel. Returns true if any of the three had data.
    async function loadServerLogsIntoResults() {
        let hasAny = false;

        // EDR alerts
        const edr = await fetchJson("/api/logs/edr");
        if (edr.ok && edr.data && Array.isArray(edr.data.alerts) && edr.data.alerts.length > 0) {
            finalAlertsBody.innerHTML = "";
            for (const alert of edr.data.alerts) appendAlertRow(finalAlertsBody, alert);
            finalAlertsEmpty.hidden = true;
            hasAny = true;
        }

        // Execution logs (pid/stdout/stderr) — may 400 if never executed
        const exec = await fetchJson("/api/logs/execution");
        if (exec.ok && exec.data) {
            if (exec.data.pid != null && !resultPid.textContent) {
                resultPid.textContent = "PID: " + exec.data.pid;
            }
            if (exec.data.stdout) { execStdout.textContent = exec.data.stdout; hasAny = true; }
            if (exec.data.stderr) { execStderr.textContent = exec.data.stderr; hasAny = true; }
        }

        // Agent logs — endpoint returns a plain-text newline-joined string
        // (not JSON), so use the raw text fallback.
        const agent = await fetchJson("/api/logs/agent");
        if (agent.ok) {
            let logsText = "";
            if (Array.isArray(agent.data)) {
                logsText = agent.data.join("\n");
            } else if (typeof agent.data === "string") {
                logsText = agent.data;
            } else {
                logsText = agent.text || "";
            }
            if (logsText.trim()) { agentLogsEl.textContent = logsText; hasAny = true; }
        }

        return hasAny;
    }

    async function renderFinalResults(execResult) {
        resultsPanel.hidden = false;

        const status = execResult.data?.status ?? "error";
        resultStatus.textContent = status;
        resultStatus.className = "badge " + status;

        if (execResult.data?.pid != null) {
            resultPid.textContent = "PID: " + execResult.data.pid;
        }
        if (execResult.data?.message) {
            resultMessage.textContent = execResult.data.message;
            resultMessage.hidden = false;
        }

        await loadServerLogsIntoResults();
    }

    // On page load: check whether the server still has data from a previous
    // detonation and, if so, restore what it can into the results panel.
    async function restorePreviousResults() {
        // Skip restore if a scan is currently in progress.
        const lock = await fetchJson("/api/lock/status");
        if (lock.ok && lock.data && lock.data.in_use === true) {
            return;
        }

        // Temporarily unhide results panel so loadServerLogsIntoResults can
        // populate it; if nothing was there we hide it again.
        resultsPanel.hidden = false;
        const hadData = await loadServerLogsIntoResults();

        if (!hadData) {
            resultsPanel.hidden = true;
            return;
        }

        // Synthesize a badge for the restored view. We don't know the exact
        // status of the previous run, so infer from the data available.
        const hasAlerts = finalAlertsBody.children.length > 0;
        const hasExecPid = resultPid.textContent !== "";
        let inferred = "previous";
        if (hasAlerts && !hasExecPid) inferred = "virus";
        else if (hasExecPid)          inferred = "ok";
        resultStatus.textContent = inferred;
        resultStatus.className = "badge " + inferred;

        // Prepend a small banner indicating this is restored, not fresh.
        const banner = document.createElement("div");
        banner.className = "muted restore-banner";
        banner.style.marginBottom = "8px";
        banner.textContent = "Showing data from the previous detonation on this agent.";
        resultsPanel.insertBefore(banner, resultsPanel.firstChild.nextSibling);
    }

    async function runDetonation(evt) {
        evt.preventDefault();

        const fileInput = $("file");
        if (!fileInput.files || fileInput.files.length === 0) return;

        const file            = fileInput.files[0];
        const dropPath        = $("drop_path").value || "C:\\Users\\Public\\Downloads\\";
        const executableArgs  = $("executable_args").value.trim();
        const executionMode   = $("execution_mode").value;
        const runtime         = Math.max(1, parseInt($("runtime").value, 10) || 10);

        resetUI();
        progressPanel.hidden = false;
        submitBtn.disabled = true;
        setStatus("Acquiring lock…");

        try {
            await acquireLock();
        } catch (err) {
            setStatus("Failed: " + err.message);
            submitBtn.disabled = false;
            return;
        }

        try {
            setStatus("Encrypting file…");
            const { blob, key } = await xorEncryptFile(file);

            setStatus(`Uploading ${file.name} (${blob.size} bytes)…`);
            const execResult = await uploadAndExecute({
                blob,
                fileName: file.name,
                dropPath,
                xorKey: key,
                executableArgs,
                executionMode,
            });

            const status = execResult.data?.status ?? "error";
            setStatus(`Execution status: ${status}`);

            if (status === "ok") {
                setStatus(`Running — polling EDR alerts for ${runtime} s…`);
                await pollEdrAlerts(runtime, 1000);
                setStatus("Runtime finished, killing process…");
                await killProcess();
            } else if (status === "virus") {
                setStatus("Detected on write — polling EDR alerts for 3 s…");
                await pollEdrAlerts(3, 1000);
            } else {
                setStatus("Execution error.");
            }

            setStatus("Fetching final logs…");
            await renderFinalResults(execResult);
            setStatus("Done.");
        } catch (err) {
            console.error(err);
            setStatus("Error: " + err.message);
            resultsPanel.hidden = false;
            resultStatus.textContent = "error";
            resultStatus.className = "badge error";
            resultMessage.textContent = err.message;
            resultMessage.hidden = false;
        } finally {
            await releaseLock();
            submitBtn.disabled = false;
        }
    }

    form.addEventListener("submit", runDetonation);

    // On initial load, try to restore what the server still holds from the
    // previous detonation. Failures are non-fatal — the fresh form is fine.
    restorePreviousResults().catch((e) => console.warn("Restore failed:", e));
})();
