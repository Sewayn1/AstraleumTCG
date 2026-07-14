const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('launcher', {
  getPlatform:         () => ipcRenderer.invoke('get-platform'),
  getLauncherVersion:  () => ipcRenderer.invoke('get-launcher-version'),
  getSettings:    () => ipcRenderer.invoke('get-settings'),
  saveSettings:   (s) => ipcRenderer.invoke('save-settings', s),
  selectFolder:   () => ipcRenderer.invoke('select-folder'),
  checkUpdate:    () => ipcRenderer.invoke('check-update'),
  startInstall:   (opts) => ipcRenderer.invoke('start-install', opts),
  launchGame:     () => ipcRenderer.invoke('launch-game'),
  uninstallGame:  () => ipcRenderer.invoke('uninstall-game'),
  minimizeWindow: () => ipcRenderer.send('window-minimize'),
  closeWindow:    () => ipcRenderer.send('window-close'),
  onInstallProgress: (cb) => {
    ipcRenderer.removeAllListeners('install-progress');
    ipcRenderer.on('install-progress', (_, data) => cb(data));
  },
  onGameClosed: (cb) => {
    ipcRenderer.removeAllListeners('game-closed');
    ipcRenderer.on('game-closed', () => cb());
  },
});
