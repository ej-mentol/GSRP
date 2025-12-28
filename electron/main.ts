import { app, BrowserWindow, ipcMain, protocol, net, shell, nativeImage, clipboard } from 'electron'
import path from 'node:path'
import fs from 'node:fs'
import { pathToFileURL } from 'node:url'
import { spawn, ChildProcess } from 'node:child_process'

process.env.DIST = path.join(__dirname, '../dist')
process.env.VITE_PUBLIC = app.isPackaged ? process.env.DIST : path.join(__dirname, '../public')

let win: BrowserWindow | null
let backendProcess: ChildProcess | null = null
const VITE_DEV_SERVER_URL = process.env['VITE_DEV_SERVER_URL']

// --- SINGLE INSTANCE LOCK ---
const gotTheLock = app.requestSingleInstanceLock()
if (!gotTheLock) {
  app.quit()
} else {
  app.on('second-instance', () => {
    if (win) {
      if (win.isMinimized()) win.restore()
      win.focus()
    }
  })
}

const GSRP_DATA_PATH = path.join(app.getPath('appData'), 'GSRP')
const ICONS_PATH = path.join(GSRP_DATA_PATH, 'icons')
const CACHE_PATH = path.join(GSRP_DATA_PATH, 'cache')
const SETTINGS_PATH = path.join(GSRP_DATA_PATH, 'settings.json')

let backendBuffer = '';

function spawnBackend() {
  const isDev = !app.isPackaged
  if (isDev) {
    const projectPath = path.join(__dirname, '../data_service/GSRP.Daemon.csproj')
    backendProcess = spawn('dotnet', ['run', '--project', projectPath])
  } else {
    const exePath = path.join(process.resourcesPath, 'bin', 'GSRP.Daemon.exe')
    backendProcess = spawn(exePath)
  }

  backendProcess.stdout?.on('data', (data) => {
    backendBuffer += data.toString()
    const lines = backendBuffer.split('\n')
    // Keep the last partial line in the buffer
    backendBuffer = lines.pop() || ''

    for (const line of lines) {
      const trimmed = line.trim()
      if (trimmed) win?.webContents.send('backend-message', trimmed)
    }
  })

  backendProcess.stderr?.on('data', (data) => {
    const lines = data.toString().split('\n')
    for (const line of lines) {
      const trimmed = line.trim()
      if (trimmed) win?.webContents.send('backend-message', JSON.stringify({ type: "CONSOLE_LOG", data: { tag: "SYS", text: "DEMON_ERR: " + trimmed } }))
    }
  })

  backendProcess.on('exit', (code) => {
    console.log(`Backend process exited with code ${code}`)
    if (code !== 0 && code !== null) {
      win?.webContents.send('backend-crash', code)
      // Retry spawning after 3 seconds
      console.log('Attempting to restart backend in 3s...');
      setTimeout(spawnBackend, 3000);
    }
  })
}

function registerIpcHandlers() {
  ipcMain.on('open-external', (_, url) => shell.openExternal(url))

  ipcMain.on('window-control', (_, action) => {
    if (!win) return
    switch (action) {
      case 'minimize': win.minimize(); break
      case 'maximize': win.isMaximized() ? win.unmaximize() : win.maximize(); break
      case 'close': win.close(); break
    }
  })

  ipcMain.on('copy-image', (_, dataUrl) => {
    try {
      const img = nativeImage.createFromDataURL(dataUrl)
      clipboard.writeImage(img)
    } catch (e) { console.error('Failed to copy image', e) }
  })

  ipcMain.handle('get-setting', (_, key) => {
    try {
      if (fs.existsSync(SETTINGS_PATH)) {
        const settings = JSON.parse(fs.readFileSync(SETTINGS_PATH, 'utf-8'));
        // Case-insensitive lookup
        const foundKey = Object.keys(settings).find(k => k.toLowerCase() === key.toLowerCase());
        return foundKey ? settings[foundKey] : null;
      }
    } catch (e) { }
    return null
  })

  ipcMain.on('save-setting', (_, { key, value }) => {
    try {
      const dir = path.dirname(SETTINGS_PATH)
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
      const settings = fs.existsSync(SETTINGS_PATH) ? JSON.parse(fs.readFileSync(SETTINGS_PATH, 'utf-8')) : {}
      settings[key] = value
      fs.writeFileSync(SETTINGS_PATH, JSON.stringify(settings, null, 2))
    } catch (e) { }
  })


  ipcMain.handle('scan-icons', async () => {
    try {
      if (!fs.existsSync(ICONS_PATH)) fs.mkdirSync(ICONS_PATH, { recursive: true })
      return fs.readdirSync(ICONS_PATH).filter(file => ['.png', '.svg'].includes(path.extname(file).toLowerCase()))
    } catch (e) { return [] }
  })

  ipcMain.on('to-backend', (_, message) => backendProcess?.stdin?.write(message + '\n'))
}

function createWindow() {
  // Protocol for ICONS
  protocol.handle('gsrp-icon', async (request) => {
    try {
      const parsed = new URL(request.url)
      // Filename is either in hostname (gsrp-icon://file.png) or pathname (gsrp-icon:///file.png)
      const filename = decodeURIComponent(parsed.hostname || parsed.pathname.replace(/^\//, ''))
      let filePath = path.join(ICONS_PATH, filename)
      if (!fs.existsSync(filePath)) {
        if (fs.existsSync(filePath + '.png')) filePath += '.png'
        else return new Response(null, { status: 404 })
      }
      return net.fetch(pathToFileURL(filePath).toString())
    } catch (e) { return new Response(null, { status: 500 }) }
  })

  // Protocol for CACHED AVATARS
  protocol.handle('gsrp-cache', async (request) => {
    try {
      const parsed = new URL(request.url)
      const filename = decodeURIComponent(parsed.hostname || parsed.pathname.replace(/^\//, ''))
      const filePath = path.join(CACHE_PATH, filename)
      if (!fs.existsSync(filePath)) return new Response(null, { status: 404 })
      return net.fetch(pathToFileURL(filePath).toString())
    } catch (e) { return new Response(null, { status: 500 }) }
  })

  win = new BrowserWindow({
    width: 1200, height: 800, minWidth: 800, minHeight: 600, frame: false, backgroundColor: '#1e1f23',
    titleBarStyle: 'hidden',
    icon: path.join(process.env.VITE_PUBLIC!, 'electron-vite.svg'),
    webPreferences: { preload: path.join(__dirname, 'preload.js') },
  })

  if (VITE_DEV_SERVER_URL) win.loadURL(VITE_DEV_SERVER_URL)
  else win.loadFile(path.join(process.env.DIST!, 'index.html'))

  spawnBackend()
}

app.on('window-all-closed', () => {
  backendProcess?.kill()
  if (process.platform !== 'darwin') app.quit()
})

app.whenReady().then(() => {
  registerIpcHandlers()
  createWindow()
})
