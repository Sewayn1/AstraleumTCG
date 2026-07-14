/* ── State ───────────────────────────────────────────────────────────────── */
let state = {
  isInstalled: false,
  hasUpdate: false,
  manifest: null,
  localVersion: '0.0.0',
  isWorking: false,
};

/* ── Boot ────────────────────────────────────────────────────────────────── */
async function init() {
  setStatus('checking', 'Vérification…');
  setBtn('', 'Vérification…', true);

  const launcherVersion = await window.launcher.getLauncherVersion();
  document.getElementById('launcher-version').textContent = 'v' + launcherVersion;

  const settings = await window.launcher.getSettings();
  document.getElementById('game-path-input').value = settings.gamePath;

  const result = await window.launcher.checkUpdate();

  if (result.error) {
    setStatus('error', 'Hors ligne');
    renderChangelog(null, result.error);
    setBtn('', 'Jouer ▶', false, 'btn-play');
    return;
  }

  state.isInstalled  = result.isInstalled;
  state.hasUpdate    = result.hasUpdate;
  state.manifest     = result.manifest;
  state.localVersion = result.localVersion;

  document.getElementById('version-display').textContent = 'v' + result.localVersion;
  renderChangelog(result.manifest.changelog);

  if (!result.isInstalled) {
    setStatus('install', 'Non installé');
    setBtn('install', 'Installer');
  } else if (result.hasUpdate) {
    setStatus('update', `v${result.manifest.version} dispo`);
    setBtn('update', 'Mettre à jour');
  } else {
    setStatus('ready', 'À jour');
    setBtn('play', 'Jouer ▶', false, 'btn-play');
  }
}

/* ── Status helpers ──────────────────────────────────────────────────────── */
function setStatus(type, text) {
  const badge = document.getElementById('status-badge');
  badge.className = 'status-badge status-' + type;
  document.getElementById('status-text').textContent = text;
}

function setBtn(mode, label, disabled = false, extraClass = '') {
  const btn = document.getElementById('btn-action');
  btn.className = 'btn-action' + (extraClass ? ' ' + extraClass : '');
  btn.textContent = label;
  btn.disabled = disabled;
  btn.dataset.mode = mode;
}

/* ── Changelog ───────────────────────────────────────────────────────────── */
function renderChangelog(changelog, error) {
  const container = document.getElementById('changelog-container');

  if (error) {
    container.innerHTML = `<p class="cl-error">Impossible de charger les notes de patch.<br><small>${escHtml(error)}</small></p>`;
    return;
  }

  if (!changelog || changelog.length === 0) {
    container.innerHTML = '<p class="cl-placeholder">Aucune note de patch disponible.</p>';
    return;
  }

  container.innerHTML = changelog.map(entry => `
    <div class="cl-entry">
      <div class="cl-entry-header">
        <span class="cl-version">${escHtml(entry.version)}</span>
        <span class="cl-title">${escHtml(entry.title || '')}</span>
        <span class="cl-date">${fmtDate(entry.date)}</span>
      </div>
      <ul class="cl-entries">
        ${(entry.entries || []).map(e => `<li>${escHtml(e)}</li>`).join('')}
      </ul>
    </div>
  `).join('');
}

function fmtDate(str) {
  try {
    return new Date(str).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
  } catch {
    return str || '';
  }
}

function escHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/* ── Progress ────────────────────────────────────────────────────────────── */
window.launcher.onInstallProgress((data) => {
  const area = document.getElementById('progress-area');
  const fill = document.getElementById('progress-fill');
  const info = document.getElementById('progress-info');

  area.classList.add('visible');
  fill.style.width = (data.percent || 0) + '%';

  if (data.stage === 'downloading') {
    const part = data.fileCount > 1
      ? `[${data.fileIndex}/${data.fileCount}] ${data.file}`
      : data.file;
    const size = data.total
      ? ` — ${toMb(data.downloaded)} / ${toMb(data.total)} Mo`
      : '';
    info.textContent = `Téléchargement : ${part}${size}  ${data.percent}%`;
  } else if (data.stage === 'extracting') {
    fill.style.width = '100%';
    info.textContent = 'Extraction…';
  }
});

function toMb(b) { return (b / 1048576).toFixed(1); }

/* ── Action button ───────────────────────────────────────────────────────── */
document.getElementById('btn-action').addEventListener('click', async () => {
  const btn  = document.getElementById('btn-action');
  const mode = btn.dataset.mode;

  if (mode === 'play') {
    btn.disabled = true;
    btn.textContent = 'En jeu…';
    const result = await window.launcher.launchGame();
    if (result.error) {
      alert('Impossible de lancer le jeu :\n' + result.error);
      setBtn('play', 'Jouer ▶', false, 'btn-play');
    }
    return;
  }

  if ((mode === 'install' || mode === 'update') && !state.isWorking) {
    state.isWorking = true;
    const isFirst   = !state.isInstalled;

    setStatus('working', isFirst ? 'Installation…' : 'Mise à jour…');
    setBtn('', isFirst ? 'Installation…' : 'Mise à jour…', true);

    const result = await window.launcher.startInstall({ isFirstInstall: isFirst });
    state.isWorking = false;

    document.getElementById('progress-area').classList.remove('visible');
    document.getElementById('progress-fill').style.width = '0%';

    if (result.error) {
      setStatus('error', 'Erreur');
      setBtn(mode, 'Réessayer');
      alert('Erreur lors de l\'installation :\n' + result.error);
      return;
    }

    state.isInstalled  = true;
    state.hasUpdate    = false;
    state.localVersion = result.version;
    document.getElementById('version-display').textContent = 'v' + result.version;
    setStatus('ready', 'À jour');
    setBtn('play', 'Jouer ▶', false, 'btn-play');
  }
});

/* ── Game closed ─────────────────────────────────────────────────────────── */
window.launcher.onGameClosed(() => {
  setBtn('play', 'Jouer ▶', false, 'btn-play');
  setStatus('ready', 'À jour');
});

/* ── Settings ────────────────────────────────────────────────────────────── */
document.getElementById('btn-settings').addEventListener('click', (e) => {
  e.stopPropagation();
  document.getElementById('settings-panel').classList.toggle('hidden');
});

document.getElementById('btn-close-settings').addEventListener('click', () => {
  document.getElementById('settings-panel').classList.add('hidden');
});

document.getElementById('btn-browse').addEventListener('click', async () => {
  const p = await window.launcher.selectFolder();
  if (p) document.getElementById('game-path-input').value = p;
});

document.getElementById('btn-uninstall').addEventListener('click', async () => {
  const result = await window.launcher.uninstallGame();
  if (result?.success) {
    document.getElementById('settings-panel').classList.add('hidden');
    await init();
  }
});

document.getElementById('btn-save-settings').addEventListener('click', async () => {
  const gamePath = document.getElementById('game-path-input').value;
  await window.launcher.saveSettings({ gamePath });
  document.getElementById('settings-panel').classList.add('hidden');
  init();
});

document.addEventListener('click', (e) => {
  const panel = document.getElementById('settings-panel');
  if (!panel.classList.contains('hidden') &&
      !panel.contains(e.target) &&
      e.target.id !== 'btn-settings') {
    panel.classList.add('hidden');
  }
});

/* ── Window controls ─────────────────────────────────────────────────────── */
document.getElementById('btn-wm-min').addEventListener('click', () => window.launcher.minimizeWindow());
document.getElementById('btn-wm-close').addEventListener('click', () => window.launcher.closeWindow());

/* ── Start ───────────────────────────────────────────────────────────────── */
init();
