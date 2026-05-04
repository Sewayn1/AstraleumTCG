const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('launcher', {
  getPlatform: () => ipcRenderer.invoke('get-platform'),
  getSettings: () => ipcRenderer.invoke('get-settings'),
  saveSettings: (s) => ipcRenderer.invoke('save-settings', s),
  selectFolder: () => ipcRenderer.invoke('select-folder'),
  checkUpdate: () => ipcRenderer.invoke('check-update'),
  installUpdate: (url) => ipcRenderer.invoke('install-update', url),
  launchGame: () => ipcRenderer.invoke('launch-game'),
  minimizeWindow: () => ipcRenderer.send('window-minimize'),
  closeWindow: () => ipcRenderer.send('window-close'),
  onDownloadProgress: (cb) => {
    ipcRenderer.removeAllListeners('download-progress');
    ipcRenderer.on('download-progress', (_, data) => cb(data));
  },
});
