const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const https = require('https');
const http = require('http');
const fs = require('fs');
const crypto = require('crypto');
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
    height: 580,
    resizable: false,
    frame: false,
    icon: path.join(__dirname, '../img/logo2.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
    backgroundColor: '#080818',
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

ipcMain.handle('get-platform',         () => process.platform)
ipcMain.handle('get-launcher-version', () => app.getVersion());
ipcMain.handle('get-settings', () => loadSettings());

ipcMain.handle('save-settings', (_, settings) => {
  saveSettings(settings);
  return { success: true };
});

ipcMain.handle('select-folder', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openDirectory'],
    title: 'Dossier du jeu',
  });
  return result.canceled ? null : result.filePaths[0];
});

ipcMain.handle('check-update', async () => {
  try {
    const manifest = await fetchJson(config.manifestUrl + '?_=' + Date.now());
    const { gamePath } = loadSettings();
    const exeName = config.gameExe[process.platform];
    const isInstalled = !!exeName && fs.existsSync(path.join(gamePath, exeName));
    const localVersion = getLocalVersion(gamePath);

    if (manifest.launcher && compareVersions(manifest.launcher.version, app.getVersion()) > 0) {
      await promptLauncherUpdate(manifest.launcher);
    }

    return {
      manifest,
      isInstalled,
      localVersion,
      hasUpdate: isInstalled && compareVersions(manifest.version, localVersion) > 0,
    };
  } catch (err) {
    return { error: err.message };
  }
});

ipcMain.handle('start-install', async (_, { isFirstInstall }) => {
  try {
    const manifest = await fetchJson(config.manifestUrl + '?_=' + Date.now());
    const { gamePath } = loadSettings();
    fs.mkdirSync(gamePath, { recursive: true });

    await downloadAndExtract(manifest.fullPackage.url, gamePath, manifest.fullPackage.size || 0);

    fs.writeFileSync(
      path.join(gamePath, 'version.json'),
      JSON.stringify({ version: manifest.version }, null, 2)
    );

    return { success: true, version: manifest.version };
  } catch (err) {
    return { error: err.message };
  }
});

ipcMain.handle('launch-game', () => {
  const { gamePath } = loadSettings();
  const exeName = config.gameExe[process.platform];
  if (!exeName) return { error: `Plateforme non supportée : ${process.platform}` };

  const exePath = path.join(gamePath, exeName);
  if (!fs.existsSync(exePath)) {
    return { error: `Jeu introuvable :\n${exePath}` };
  }

  spawn(exePath, [], { detached: true, stdio: 'ignore' }).unref();
  app.quit();
  return { success: true };
});

// ── Launcher self-update ───────────────────────────────────────────────────────

async function promptLauncherUpdate(launcherInfo) {
  const result = await dialog.showMessageBox(mainWindow, {
    type: 'info',
    title: 'Mise à jour du Launcher',
    message: 'Mise à jour du Launcher nécessaire',
    detail: `La version ${launcherInfo.version} est disponible.\nLe Launcher va se mettre à jour et redémarrer automatiquement.`,
    buttons: ['Mettre à jour', 'Plus tard'],
    defaultId: 0,
    cancelId: 1,
  });

  if (result.response === 0) {
    try {
      await downloadAndReplaceLauncher(launcherInfo);
    } catch (err) {
      await dialog.showMessageBox(mainWindow, {
        type: 'error',
        title: 'Erreur de mise à jour',
        message: 'La mise à jour du Launcher a échoué.',
        detail: err.message,
        buttons: ['OK'],
      });
    }
  }
}

async function downloadAndReplaceLauncher(launcherInfo) {
  const currentExe = process.execPath;
  const tempExe    = currentExe + '.update';

  await downloadFile(launcherInfo.url, tempExe, (p) => {
    sendProgress({
      stage: 'downloading',
      file: `Launcher v${launcherInfo.version}`,
      fileIndex: 1, fileCount: 1,
      percent: p.percent, downloaded: p.downloaded, total: p.total,
    });
  });

  const batchPath = path.join(app.getPath('temp'), 'astraleum-launcher-update.bat');
  const pid       = process.pid;

  const batch = [
    '@echo off',
    `set "MYPID=${pid}"`,
    `set "NEWEXE=${tempExe}"`,
    `set "OLDEXE=${currentExe}"`,
    ':waitloop',
    'tasklist /FI "PID eq %MYPID%" /NH 2>NUL | findstr /I "exe" >NUL 2>&1',
    'if not errorlevel 1 (',
    '  timeout /t 1 /nobreak >NUL',
    '  goto waitloop',
    ')',
    'timeout /t 1 /nobreak >NUL',
    'move /Y "%NEWEXE%" "%OLDEXE%"',
    'start "" "%OLDEXE%"',
    '(goto) 2>nul & del "%~f0"',
  ].join('\r\n');

  fs.writeFileSync(batchPath, batch, 'latin1');
  spawn('cmd.exe', ['/C', batchPath], { detached: true, stdio: 'ignore', windowsHide: true }).unref();
  setTimeout(() => app.quit(), 800);
}

// ── Install helpers ────────────────────────────────────────────────────────────

async function downloadAndExtract(url, gamePath, totalSize) {
  const zipPath = path.join(app.getPath('temp'), 'astraleum-install.zip');

  sendProgress({ stage: 'downloading', file: 'Astraleum.zip', fileIndex: 1, fileCount: 1, percent: 0, downloaded: 0, total: totalSize });

  await downloadFile(url, zipPath, (p) => {
    sendProgress({
      stage: 'downloading',
      file: 'Astraleum.zip',
      fileIndex: 1,
      fileCount: 1,
      percent: p.percent,
      downloaded: p.downloaded,
      total: p.total || totalSize,
    });
  });

  sendProgress({ stage: 'extracting', percent: 100 });

  const zip = new AdmZip(zipPath);
  const entries = zip.getEntries();
  const topFolders = new Set(entries.map(e => e.entryName.split('/')[0]));
  const hasRoot = topFolders.size === 1 && entries.some(e => !e.isDirectory);

  if (hasRoot && entries.length > 1) {
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

  try { fs.unlinkSync(zipPath); } catch {}
}

async function downloadChangedFiles(manifest, gamePath) {
  const filesToUpdate = [];

  for (const file of manifest.files) {
    const localPath = path.join(gamePath, file.path.replace(/\//g, path.sep));
    const localHash = await computeFileSha256(localPath);
    if (localHash !== file.sha256) {
      filesToUpdate.push(file);
    }
  }

  if (filesToUpdate.length === 0) return;

  const totalSize = filesToUpdate.reduce((sum, f) => sum + (f.size || 0), 0);
  let bytesCompleted = 0;

  for (let i = 0; i < filesToUpdate.length; i++) {
    const file = filesToUpdate[i];
    const destPath = path.join(gamePath, file.path.replace(/\//g, path.sep));
    fs.mkdirSync(path.dirname(destPath), { recursive: true });

    const tmpPath = destPath + '.tmp';

    await downloadFile(file.url, tmpPath, (p) => {
      sendProgress({
        stage: 'downloading',
        file: file.path.split('/').pop(),
        fileIndex: i + 1,
        fileCount: filesToUpdate.length,
        percent: totalSize ? Math.round(((bytesCompleted + p.downloaded) / totalSize) * 100) : p.percent,
        downloaded: bytesCompleted + p.downloaded,
        total: totalSize,
      });
    });

    bytesCompleted += file.size || 0;

    try { fs.unlinkSync(destPath); } catch {}
    fs.renameSync(tmpPath, destPath);
  }
}

function sendProgress(data) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('install-progress', data);
  }
}

// ── File helpers ───────────────────────────────────────────────────────────────

function computeFileSha256(filePath) {
  return new Promise((resolve) => {
    try {
      const hash = crypto.createHash('sha256');
      const stream = fs.createReadStream(filePath);
      stream.on('data', chunk => hash.update(chunk));
      stream.on('end', () => resolve(hash.digest('hex')));
      stream.on('error', () => resolve(null));
    } catch {
      resolve(null);
    }
  });
}

function getLocalVersion(gamePath) {
  const versionFile = path.join(gamePath, 'version.json');
  try {
    if (fs.existsSync(versionFile))
      return JSON.parse(fs.readFileSync(versionFile, 'utf8')).version || '0.0.0';
  } catch {}
  return '0.0.0';
}

function compareVersions(a, b) {
  const pa = String(a).split('.');
  const pb = String(b).split('.');
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const sa = pa[i] || '0';
    const sb = pb[i] || '0';
    const na = parseInt(sa, 10) || 0;
    const nb = parseInt(sb, 10) || 0;
    if (na !== nb) return na > nb ? 1 : -1;
    const sufA = sa.replace(/^\d+/, '');
    const sufB = sb.replace(/^\d+/, '');
    if (sufA !== sufB) return sufA > sufB ? 1 : -1;
  }
  return 0;
}

function fetchJson(url) {
  return new Promise((resolve, reject) => {
    const get = url.startsWith('https') ? https.get : http.get;
    get(url, { headers: { 'User-Agent': 'Astraleum-Launcher', 'Cache-Control': 'no-cache', 'Pragma': 'no-cache' } }, (res) => {
      if (res.statusCode === 301 || res.statusCode === 302) {
        return fetchJson(res.headers.location).then(resolve).catch(reject);
      }
      let data = '';
      res.on('data', c => data += c);
      res.on('end', () => {
        try { resolve(JSON.parse(data)); }
        catch { reject(new Error('Réponse invalide depuis le serveur')); }
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
        return reject(new Error(`HTTP ${res.statusCode} lors du téléchargement`));
      }

      const total = parseInt(res.headers['content-length'] || '0', 10);
      let downloaded = 0;
      const file = fs.createWriteStream(dest);

      res.on('data', (chunk) => {
        downloaded += chunk.length;
        onProgress({ percent: total ? Math.round((downloaded / total) * 100) : 0, downloaded, total });
      });

      res.pipe(file);
      file.on('finish', () => file.close(resolve));
      file.on('error', (err) => { try { fs.unlinkSync(dest); } catch {} reject(err); });
    }).on('error', reject);
  });
}
