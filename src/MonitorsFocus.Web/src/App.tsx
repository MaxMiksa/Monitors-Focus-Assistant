import { useCallback, useEffect, useRef, useState } from 'react'
import { AnimatePresence, MotionConfig, motion } from 'motion/react'
import { Monitor, Laptop, Sun, Moon, Focus, Thermometer, Link2, Pause, Play, Settings2, Clock3, RotateCcw, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Slider } from '@/components/ui/slider'
import { Switch } from '@/components/ui/switch'
import { isDesktop, preview, request, resizePanel } from '@/lib/control'
import type { Display, Request, Snapshot } from '@/lib/control'
import { SettingsEditor } from '@/components/settings-editor'

const pages = [{ name: '显示器', icon: Monitor }, { name: '场景', icon: Sun }, { name: '时间计划', icon: Clock3 }, { name: '专注', icon: Focus }, { name: '通用', icon: Settings2 }]
const sceneIcons = [Sun, Moon, Focus]
export type Send = (command: Request) => Promise<Snapshot | null>
export function Level({ value, max = 100, min = 0, step = 1, label, disabled, temperature, onCommit }: { value: number; max?: number; min?: number; step?: number; label: string; disabled?: boolean; temperature?: boolean; onCommit: (value: number) => void | Promise<unknown> }) {
  const [draft, setDraft] = useState<number | null>(null)
  return <Slider aria-label={label} aria-valuetext={`${draft ?? value}${temperature ? ' K' : '%'}`} className={temperature ? 'temperature' : ''} value={[draft ?? value]} min={min} max={max} step={step} disabled={disabled}
    onValueChange={([v]) => setDraft(v)} onValueCommit={async ([v]) => { try { await onCommit(v) } finally { setDraft(null) } }} />
}
export function DisplayRow({ display, onCommit }: { display: Display; onCommit: (value: number) => void | Promise<unknown> }) {
  const Icon = display.Name.includes('笔记本') ? Laptop : Monitor
  return <div><div className="display-row"><Icon /><span>{display.Name}</span><Level label={`${display.Name}亮度`} value={display.Brightness} disabled={!display.CanAdjust || !!display.Error} onCommit={onCommit} /><Sun className="text-muted-foreground" /><output>{display.Brightness}%</output></div>
    {display.Error && <p className="error" role="status">{display.Error}</p>}</div>
}
function QuickPanel({ snapshot, send, openSettings }: { snapshot: Snapshot; send: Send; openSettings: () => void }) {
  const supported = snapshot.Displays.filter(d => d.CanAdjustColor)
  const kelvin = supported[0]?.Kelvin
  return <div className="quick"><main className="quick-main">
    {snapshot.Displays.length === 0 && <p className="caption py-6">正在检测显示器…</p>}
    {snapshot.Displays.map(display => <DisplayRow key={display.Id} display={display} onCommit={Value => send({ Action: 'brightness', Id: display.Id, Value })} />)}
    <div className="binding"><Link2 /><Switch id="link" checked={snapshot.Settings.Linked} disabled={snapshot.Displays.filter(d => d.CanAdjust).length < 2} onCheckedChange={on => void send({ Action: 'link', Value: on ? 1 : 0 })} /><label htmlFor="link">保持比例</label></div>
    <div className="temperature-row"><Thermometer /><span>色温</span><Level label="硬件色温" value={kelvin ?? 6500} min={2700} max={6500} step={100} disabled={!supported.length} temperature onCommit={Value => send({ Action: 'temperature', Value })} /><output className="text-right">{kelvin ? `${kelvin} K` : '原色'}</output></div>
    <p className="caption temperature-note">{supported.length ? `仅调节 ${supported.map(d => d.Name).join('、')} · 色温为近似值` : '硬件色温暂不可用，可在设置中重新检测'}</p>
    <section className="scenes"><p className="text-sm font-medium">场景</p><div className="scene-line"><div className="scene-buttons">{snapshot.Settings.Scenes.map((scene, i) => { const Icon = sceneIcons[i % sceneIcons.length]; return <Button variant="outline" key={scene.Id} onClick={() => void send({ Action: 'scene', Id: scene.Id })}><Icon />{scene.Name}</Button> })}</div><div className="source"><p className="caption">当前来源</p><p>{snapshot.Source}</p></div></div></section>
  </main><footer className="footer"><Button variant="outline" onClick={() => void send({ Action: 'pause' })}>{snapshot.FocusPaused ? <Play /> : <Pause />}{snapshot.FocusPaused ? '恢复专注' : '暂停专注'}</Button><Button variant="ghost" disabled={!supported.length || supported.every(d => d.Kelvin === null)} onClick={() => void send({ Action: 'restore-color' })}><RotateCcw />恢复原色</Button><Button variant="ghost" className="settings-button" onClick={openSettings}><Settings2 />设置</Button></footer></div>
}
export default function App() {
  const demo = !isDesktop && new URLSearchParams(location.search).get('preview') === '1'
  const [snapshot, setSnapshot] = useState<Snapshot | null>(demo ? structuredClone(preview) : null)
  const [error, setError] = useState('')
  const [page, setPage] = useState('显示器')
  const queue = useRef(Promise.resolve())
  const busy = useRef(0)
  const send = useCallback<Send>((command) => {
    if (demo) { setError('视觉预览不控制硬件；请在桌面窗口体验实际操作。'); return Promise.resolve(null) }
    busy.current++
    const task = queue.current.then(async () => {
      try { const next = await request(command); setSnapshot(next); setError(previous => command.Action !== 'snapshot' ? next.Error ?? '' : next.Error ?? (previous.startsWith('连接失败：') ? '' : previous)); return next.Error ? null : next }
      catch (e) { setError('连接失败：' + (e instanceof Error ? e.message : String(e))); return null }
      finally { busy.current-- }
    })
    queue.current = task.then(() => {})
    return task
  }, [demo])
  useEffect(() => {
    if (demo || !isDesktop) return
    const initial = setTimeout(() => void send({ Action: 'snapshot' }), 0)
    const timer = setInterval(() => { if (!document.hidden && !busy.current) void send({ Action: 'snapshot' }) }, 2000)
    return () => { clearTimeout(initial); clearInterval(timer) }
  }, [send, demo])
  function navigate(next: string) { setPage(next); resizePanel(next !== '显示器'); setError('') }
  return <MotionConfig reducedMotion="user"><AnimatePresence mode="wait" initial={false}><motion.div key={page === '显示器' ? 'quick' : 'settings'} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: .15 }}>
    {!snapshot ? <div className="p-7"><h1>Monitors Focus</h1><p className="caption my-6">{error || '正在连接控制核心…'}</p><Button variant="outline" onClick={() => void send({ Action: 'snapshot' })}><RefreshCw />重新连接</Button></div> : page === '显示器' ? <QuickPanel snapshot={snapshot} send={send} openSettings={() => navigate('时间计划')} /> : <div className="settings"><nav className="sidebar" aria-label="设置导航">{pages.map(({ name, icon: Icon }) => <Button key={name} variant={page === name ? 'secondary' : 'ghost'} aria-current={page === name ? 'page' : undefined} onClick={() => navigate(name)}><Icon />{name}</Button>)}</nav><main className="settings-main"><SettingsEditor page={page} snapshot={snapshot} send={send} /></main></div>}
  </motion.div></AnimatePresence>{snapshot?.Warning && <p className="caption px-6 py-2" role="status">{snapshot.Warning}</p>}{snapshot && error && <p role="alert" className="error">{error}</p>}{demo && <p className="caption px-7 pb-3">视觉预览 · 示例数据 · 不连接硬件</p>}</MotionConfig>
}
