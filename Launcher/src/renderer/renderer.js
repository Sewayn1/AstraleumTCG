/* ── State ───────────────────────────────────────────────────────────── */
let remoteData = null;
let platform   = 'win32';

/* ── Boot ────────────────────────────────────────────────────────────── */
async function init() {
  platform = await window.launcher.getPlatform();

  const settings = await window.launcher.getSettings();
  document.getElementById('game-path-input').value = settings.gamePath;

  setVersionLabel('Checking for updates…');

  const result = await window.launcher.checkUpdate();

  if (result.error) {
    setVersionLabel('Version: unknown');
    setUpdateLabel('Could not check for updates', 'error');
    enablePlay();
    return;
  }

  remoteData = result.remote;
  setVersionLabel(`v${result.local}`);

  if (result.hasUpdate) {
    setUpdateLabel(`Update available — v${result.remote.version}`, 'warn');
    showUpdateModal(result.local, result.remote);
  } else {
    setUpdateLabel('Up to date');
    enablePlay();
  }
}

/* ── Version helpers ─────────────────────────────────────────────────── */
function setVersionLabel(text) {
  document.getElementById('version-label').textContent = text;
}
function setUpdateLabel(text, cls = '') {
  const el = document.getElementById('update-label');
  el.textContent = text;
  el.className = cls;
}
function enablePlay() {
  document.getElementById('btn-play').disabled = false;
}

/* ── Update modal ────────────────────────────────────────────────────── */
function showUpdateModal(oldVer, remote) {
  document.getElementById('ver-old').textContent = `v${oldVer}`;
  document.getElementById('ver-new').textContent = `v${remote.version}`;
  document.getElementById('release-notes').textContent = remote.notes || '';
  document.getElementById('overlay-update').classList.remove('hidden');
}

document.getElementById('btn-update').addEventListener('click', async () => {
  const url = remoteData?.files?.[platform]?.url;
  if (!url) {
    alert('No download found for your platform.');
    return;
  }

  document.getElementById('modal-actions').classList.add('hidden');
  document.getElementById('progress-wrap').classList.remove('hidden');

  window.launcher.onDownloadProgress((data) => {
    const fill = document.getElementById('progress-fill');
    const text = document.getElementById('progress-text');

    if (data.stage === 'downloading') {
      fill.style.width = data.percent + '%';
      const dlMB  = (data.downloaded / 1048576).toFixed(1);
      const totMB = data.total ? (data.total / 1048576).toFixed(1) : '?';
      text.textContent = `Downloading… ${data.percent}%  (${dlMB} / ${totMB} MB)`;
    } else if (data.stage === 'extracting') {
      fill.style.width = '100%';
      text.textContent = 'Installing…';
    }
  });

  const result = await window.launcher.installUpdate(url);

  if (result.error) {
    document.getElementById('progress-text').textContent = '⚠ Error: ' + result.error;
    return;
  }

  document.getElementById('overlay-update').classList.add('hidden');
  setVersionLabel(`v${remoteData.version}`);
  setUpdateLabel('Up to date');
  enablePlay();
});

document.getElementById('btn-skip').addEventListener('click', () => {
  document.getElementById('overlay-update').classList.add('hidden');
  setUpdateLabel(`v${remoteData.version} available (skipped)`, 'warn');
  enablePlay();
});

/* ── Play ────────────────────────────────────────────────────────────── */
document.getElementById('btn-play').addEventListener('click', async () => {
  document.getElementById('btn-play').disabled = true;
  document.getElementById('btn-play').textContent = 'Starting…';

  const result = await window.launcher.launchGame();
  if (result.error) {
    alert('Could not launch:\n' + result.error);
    document.getElementById('btn-play').disabled = false;
    document.getElementById('btn-play').textContent = 'PLAY';
  }
});

/* ── Settings ────────────────────────────────────────────────────────── */
document.getElementById('btn-settings').addEventListener('click', (e) => {
  e.stopPropagation();
  document.getElementById('settings-panel').classList.toggle('hidden');
});

document.getElementById('btn-close-settings').addEventListener('click', closeSettings);

document.getElementById('btn-browse').addEventListener('click', async () => {
  const p = await window.launcher.selectFolder();
  if (p) document.getElementById('game-path-input').value = p;
});

document.getElementById('btn-save-settings').addEventListener('click', async () => {
  const gamePath = document.getElementById('game-path-input').value;
  await window.launcher.saveSettings({ gamePath });
  closeSettings();
  init();
});

document.addEventListener('click', (e) => {
  const panel = document.getElementById('settings-panel');
  if (!panel.contains(e.target) && e.target.id !== 'btn-settings') {
    panel.classList.add('hidden');
  }
});

function closeSettings() {
  document.getElementById('settings-panel').classList.add('hidden');
}

/* ── Window controls ─────────────────────────────────────────────────── */
document.getElementById('btn-wm-min').addEventListener('click',   () => window.launcher.minimizeWindow());
document.getElementById('btn-wm-close').addEventListener('click', () => window.launcher.closeWindow());

/* ── Start ───────────────────────────────────────────────────────────── */
init();
