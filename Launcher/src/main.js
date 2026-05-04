const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const https = require('https');
const http = require('http');
const fs = require('fs');
const { spawn } = require('child_process');
const AdmZip = require('adm-zip');

const config = require('../launcher.config.json');
const SETTINGS_FILE = path.join(app.getPath('userData'), 'settings.json');

let mainWindow;

// ── Settings ──────────────────────────────────────────────────────────────────

function loadSettings() {
  try {
    if (fs.existsSync(SETTINGS_FILE)) return JSON.parse(fs.readFileSync(SETTINGS_FILE, 'utf8'));
  } catch {}
  return { gamePath: path.join(app.getPath('userData'), 'game') };
}

function saveSettings(settings) {
  fs.mkdirSync(path.dirname(SETTINGS_FILE), { recursive: true });
  fs.writeFileSync(SETTINGS_FILE, JSON.stringify(settings, null, 2));
}

// ── Window ─────────────────────────────────────────────────────────────────────

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 900,
    height: 560,
    resizable: false,
    frame: false,
    icon: path.join(__dirname, '../img/logo2.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
    backgroundColor: '#0a0a1a',
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

// ── IPC ────────────────────────────────────────────────────────────────────────

ipcMain.on('window-minimize', () => mainWindow.minimize());
ipcMain.on('window-close', () => app.quit());

ipcMain.handle('get-platform', () => process.platform);

ipcMain.handle('get-settings', () => loadSettings());

ipcMain.handle('save-settings', (_, settings) => {
  saveSettings(settings);
  return { success: true };
});

ipcMain.handle('select-folder', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openDirectory'],
    title: 'Select Game Folder',
  });
  return result.canceled ? null : result.filePaths[0];
});

ipcMain.handle('check-update', async () => {
  try {
    const remote = await fetchJson(config.versionJsonUrl);
    const local = getLocalVersion(loadSettings().gamePath);
    return {
      hasUpdate: compareVersions(remote.version, local) > 0,
      remote,
      local,
    };
  } catch (err) {
    return { error: err.message };
  }
});

ipcMain.handle('install-update', async (event, downloadUrl) => {
  const { gamePath } = loadSettings();
  const zipPath = path.join(app.getPath('temp'), 'astraleum-update.zip');

  try {
    fs.mkdirSync(gamePath, { recursive: true });

    await downloadFile(downloadUrl, zipPath, (progress) => {
      mainWindow.webContents.send('download-progress', progress);
    });

    mainWindow.webContents.send('download-progress', { stage: 'extracting', percent: 100 });

    const zip = new AdmZip(zipPath);
    const entries = zip.getEntries();

    // Detect if all files share a common root folder (e.g. Astraleum-v1.0.0/)
    const topFolders = new Set(entries.map(e => e.entryName.split('/')[0]));
    const hasCommonRoot = topFolders.size === 1 && !entries[0].isDirectory === false;

    if (hasCommonRoot && entries.length > 1) {
      const rootFolder = [...topFolders][0] + '/';
      entries.forEach(entry => {
        if (!entry.isDirectory) {
          const relPath = entry.entryName.slice(rootFolder.length);
          if (relPath) {
            const dest = path.join(gamePath, relPath);
            fs.mkdirSync(path.dirname(dest), { recursive: true });
            fs.writeFileSync(dest, entry.getData());
          }
        }
      });
    } else {
      zip.extractAllTo(gamePath, true);
    }

    fs.unlinkSync(zipPath);

    // Persist the new version locally
    const versionFile = path.join(gamePath, 'version.json');
    const remote = await fetchJson(config.versionJsonUrl);
    fs.writeFileSync(versionFile, JSON.stringify({ version: remote.version }, null, 2));

    return { success: true };
  } catch (err) {
    try { fs.unlinkSync(zipPath); } catch {}
    return { error: err.message };
  }
});

ipcMain.handle('launch-game', () => {
  const { gamePath } = loadSettings();
  const exeName = config.gameExe[process.platform];
  if (!exeName) return { error: `Unsupported platform: ${process.platform}` };

  const exePath = path.join(gamePath, exeName);
  if (!fs.existsSync(exePath)) {
    return { error: `Game not found at:\n${exePath}` };
  }

  spawn(exePath, [], { detached: true, stdio: 'ignore' }).unref();
  app.quit();
  return { success: true };
});

// ── Helpers ────────────────────────────────────────────────────────────────────

function getLocalVersion(gamePath) {
  const versionFile = path.join(gamePath, 'version.json');
  try {
    if (fs.existsSync(versionFile))
      return JSON.parse(fs.readFileSync(versionFile, 'utf8')).version || '0.0.0';
  } catch {}
  return '0.0.0';
}

function compareVersions(a, b) {
  const pa = String(a).split('.').map(Number);
  const pb = String(b).split('.').map(Number);
  for (let i = 0; i < 3; i++) {
    if ((pa[i] || 0) > (pb[i] || 0)) return 1;
    if ((pa[i] || 0) < (pb[i] || 0)) return -1;
  }
  return 0;
}

function fetchJson(url) {
  return new Promise((resolve, reject) => {
    const get = url.startsWith('https') ? https.get : http.get;
    get(url, { headers: { 'User-Agent': 'Astraleum-Launcher' } }, (res) => {
      if (res.statusCode === 301 || res.statusCode === 302) {
        return fetchJson(res.headers.location).then(resolve).catch(reject);
      }
      let data = '';
      res.on('data', c => data += c);
      res.on('end', () => {
        try { resolve(JSON.parse(data)); }
        catch { reject(new Error('Invalid JSON from version endpoint')); }
      });
    }).on('error', reject);
  });
}

function downloadFile(url, dest, onProgress) {
  return new Promise((resolve, reject) => {
    const get = url.startsWith('https') ? https.get : http.get;
    get(url, { headers: { 'User-Agent': 'Astraleum-Launcher' } }, (res) => {
      if (res.statusCode === 301 || res.statusCode === 302) {
        return downloadFile(res.headers.location, dest, onProgress).then(resolve).catch(reject);
      }
      if (res.statusCode !== 200) {
        return reject(new Error(`HTTP ${res.statusCode} while downloading`));
      }

      const total = parseInt(res.headers['content-length'] || '0', 10);
      let downloaded = 0;
      const file = fs.createWriteStream(dest);

      res.on('data', (chunk) => {
        downloaded += chunk.length;
        onProgress({
          stage: 'downloading',
          percent: total ? Math.round((downloaded / total) * 100) : 0,
          downloaded,
          total,
        });
      });

      res.pipe(file);
      file.on('finish', () => file.close(resolve));
      file.on('error', (err) => { fs.unlink(dest, () => {}); reject(err); });
    }).on('error', reject);
  });
}
