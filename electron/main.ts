import { app, BrowserWindow, ipcMain, protocol, net, shell, nativeImage, clipboard, dialog } from 'electron'
import path from 'node:path'
import fs from 'node:fs'
import { pathToFileURL } from 'node:url'
import { spawn, ChildProcess, exec } from 'node:child_process'
import { promisify } from 'node:util'

const execAsync = promisify(exec)

// --- ENVIRONMENT ---
process.env.DIST = path.join(__dirname, '../dist')
process.env.VITE_PUBLIC = app.isPackaged ? process.env.DIST : path.join(__dirname, '../public')

let win: BrowserWindow | null = null
let backendProcess: ChildProcess | null = null
let isQuitting = false
let backendBuffer = ''

const DAEMON_PORT = 5000
const UDP_PORT = 26000 
const DAEMON_URL = `http://localhost:${DAEMON_PORT}`
const VITE_DEV_SERVER_URL = process.env['VITE_DEV_SERVER_URL']

// --- UTILS ---

function safeSend(channel: string, data: any) {
  if (win && !win.isDestroyed() && win.webContents && !win.webContents.isDestroyed()) {
    win.webContents.send(channel, data)
  }
}

async function waitForPortFree(port: number, maxAttempts = 10): Promise<boolean> {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const { stdout } = await execAsync(`netstat -ano | findstr :${port} | findstr LISTENING`)
      if (!stdout || stdout.trim().length === 0) return true
      await new Promise(resolve => setTimeout(resolve, 500))
    } catch (e) { return true }
  }
  return false
}

async function killProcessOnPort(port: number) {
  try {
    if (process.platform === 'win32') {
      const { stdout } = await execAsync(`netstat -ano | findstr :${port} | findstr LISTENING`)
      if (stdout) {
        const lines = stdout.trim().split('\n')
        for (const line of lines) {
          const parts = line.trim().split(/\s+/)
          const pid = parts.pop()
          if (pid && !isNaN(Number(pid))) {
            await execAsync(`taskkill /F /PID ${pid}`).catch(() => {})
          }
        }
        await waitForPortFree(port)
      }
    }
  } catch (e) {}
}

async function waitForDaemon(maxAttempts = 60): Promise<boolean> {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const response = await net.fetch(`${DAEMON_URL}/health`);
      if (response.ok || response.status === 202) {
        console.log('[Main] Daemon server is up.');
        return true;
      }
    } catch (e) {}
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  return false;
}

async function stopDaemon() {
  if (!backendProcess) return
  
  const proc = backendProcess
  backendProcess = null

  try {
    await net.fetch(`${DAEMON_URL}/shutdown`, { method: 'POST' }).catch(() => {})
    
    await new Promise<void>((resolve) => {
      const timer = setTimeout(() => {
        if (process.platform === 'win32') {
          exec(`taskkill /F /IM GSRP.Daemon.exe /T`, { stdio: 'ignore' })
        } else {
          proc.kill('SIGKILL')
        }
        resolve()
      }, 3000)

      proc.on('exit', () => {
        clearTimeout(timer)
        resolve()
      })
    })
  } catch (e) {}
}

async function spawnBackend() {
  const isDev = !app.isPackaged
  await killProcessOnPort(DAEMON_PORT)
  await killProcessOnPort(UDP_PORT)
  
  let exePath = ''
  let args: string[] = []
  const options: any = { 
    shell: true, 
    windowsHide: true, 
    detached: false,
    stdio: ['pipe', 'pipe', 'pipe']
  }

  if (isDev) {
    exePath = 'dotnet';
    // Убираем --no-build, чтобы dotnet сам следил за актуальностью кода
    args = ['run', '--project', path.join(__dirname, '../data_service/GSRP.Daemon.csproj')];
  } else {
    exePath = path.join(process.resourcesPath, 'bin', 'GSRP.Daemon.exe')
    options.cwd = path.join(process.resourcesPath, 'bin')
  }

  backendProcess = spawn(exePath, args, options)

  backendProcess.stdout?.on('data', (data) => {
    backendBuffer += data.toString()
    const lines = backendBuffer.split('\n')
    backendBuffer = lines.pop() || ''

    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;

      // Пытаемся понять: это наш JSON или просто текст?
      try {
        if (trimmed.startsWith('{')) {
          JSON.parse(trimmed); // Проверка на валидность
          safeSend('backend-message', trimmed);
        } else {
          throw new Error('Not JSON');
        }
      } catch (e) {
        // Это сырой текст или лог dotnet - пакуем в CONSOLE_LOG
        const wrappedLog = JSON.stringify({ 
          type: "CONSOLE_LOG", 
          data: { tag: "SYS", text: trimmed } 
        });
        safeSend('backend-message', wrappedLog);
      }
    }
  })

  backendProcess.stderr?.on('data', (data) => {
    const msg = data.toString().trim()
    if (msg) safeSend('backend-message', JSON.stringify({ type: "CONSOLE_LOG", data: { tag: "SYS", text: "DEMON_ERR: " + msg } }))
  })

  const ready = await waitForDaemon()
  if (!ready && !isQuitting) {
    dialog.showErrorBox('Core Error', 'Daemon failed to initialize.')
  }
}

// --- APP ---

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

function registerIpcHandlers() {
  ipcMain.on('window-control', (_, action) => {
    if (!win) return
    if (action === 'minimize') win.minimize()
    else if (action === 'maximize') win.isMaximized() ? win.unmaximize() : win.maximize()
    else if (action === 'close') win.close()
  })
  
  ipcMain.on('to-backend', (_, message) => {
    if (backendProcess && backendProcess.stdin && backendProcess.stdin.writable) {
      backendProcess.stdin.write(message + '\n')
    }
  })

  ipcMain.on('open-external', (_, url) => shell.openExternal(url))
  ipcMain.on('copy-image', (_, dataUrl) => {
    try {
      const img = nativeImage.createFromDataURL(dataUrl)
      clipboard.writeImage(img)
    } catch (e) {}
  })
  ipcMain.handle('get-setting', (_, key) => {
    try {
      const p = path.join(app.getPath('appData'), 'GSRP', 'settings.json')
      if (fs.existsSync(p)) {
        const s = JSON.parse(fs.readFileSync(p, 'utf-8'))
        const k = Object.keys(s).find(x => x.toLowerCase() === key.toLowerCase())
        return k ? s[k] : null
      }
    } catch (e) {}
    return null
  })
  ipcMain.on('save-setting', (_, { key, value }) => {
    try {
      const p = path.join(app.getPath('appData'), 'GSRP', 'settings.json')
      const dir = path.dirname(p)
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
      const s = fs.existsSync(p) ? JSON.parse(fs.readFileSync(p, 'utf-8')) : {}
      s[key] = value
      fs.writeFileSync(p, JSON.stringify(s, null, 2))
    } catch (e) {}
  })
  ipcMain.handle('scan-icons', async () => {
    try {
      const p = path.join(app.getPath('appData'), 'GSRP', 'icons')
      if (!fs.existsSync(p)) fs.mkdirSync(p, { recursive: true })
      return fs.readdirSync(p).filter(f => ['.png', '.svg'].includes(path.extname(f).toLowerCase()))
    } catch (e) { return [] }
  })
}

function createWindow() {
  win = new BrowserWindow({
    width: 1200, height: 800, minWidth: 800, minHeight: 600, frame: false, backgroundColor: '#1e1f23',
    titleBarStyle: 'hidden',
    icon: path.join(process.env.VITE_PUBLIC!, 'electron-vite.svg'),
    webPreferences: { preload: path.join(__dirname, 'preload.js') },
  })

  protocol.handle('gsrp-icon', (req) => {
    const file = decodeURIComponent(new URL(req.url).hostname || new URL(req.url).pathname.replace(/^\//, ''))
    const p = path.join(app.getPath('appData'), 'GSRP', 'icons', file)
    const final = fs.existsSync(p) ? p : (fs.existsSync(p + '.png') ? p + '.png' : null)
    return final ? net.fetch(pathToFileURL(final).toString()) : new Response(null, { status: 404 })
  })

  if (VITE_DEV_SERVER_URL) win.loadURL(VITE_DEV_SERVER_URL)
  else win.loadFile(path.join(__dirname, '../dist/index.html'))
}

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('will-quit', async (e) => {
  if (!isQuitting && (backendProcess || win)) {
    e.preventDefault()
    isQuitting = true
    await stopDaemon()
    app.quit()
  }
})

app.whenReady().then(async () => {
  registerIpcHandlers()
  createWindow()
  await spawnBackend()
})
