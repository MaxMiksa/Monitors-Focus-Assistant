import { useState } from 'react'
import { Plus, Trash2, RefreshCw, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { DisplayRow, Level } from '@/App'
import type { Send } from '@/App'
import type { Settings, Snapshot } from '@/lib/control'

export function SettingsEditor({ page, snapshot, send }: { page: string; snapshot: Snapshot; send: Send }) {
  const [draft, setDraft] = useState(() => structuredClone(snapshot.Settings))
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  function change(update: (settings: Settings) => void) { setDraft(current => { const next = structuredClone(current); update(next); return next }); setMessage('') }
  async function save() {
    setSaving(true)
    try { const saved = await send({ Action: 'save', Settings: draft }); if (saved) { setDraft(structuredClone(saved.Settings)); setMessage('已保存') } }
    finally { setSaving(false) }
  }
  const title = page === '时间计划' ? '让屏幕跟随一天的节奏' : page === '专注' ? '留心眼前，余下安静' : page
  return <><h1>{title}</h1>
    {page === '时间计划' && <>
      <div className="form-row"><label htmlFor="schedule">按时间自动渐变</label><Switch id="schedule" checked={draft.ScheduleEnabled} onCheckedChange={on => change(s => { s.ScheduleEnabled = on })} /></div>
      <p className="caption mt-4">到达时间点后，逐渐过渡至所选场景。每个节点可单独设置渐变时长。</p>
      <ScheduleChart settings={draft} snapshot={snapshot} />
      {draft.Schedule.map((point, i) => <div className="plan-row" key={i}><input aria-label={`时间点 ${i + 1}`} type="time" required value={`${String(Math.floor(point.MinuteOfDay / 60)).padStart(2, '0')}:${String(point.MinuteOfDay % 60).padStart(2, '0')}`} onChange={e => { if (!e.target.value) return; const [h, m] = e.target.value.split(':').map(Number); change(s => { s.Schedule[i].MinuteOfDay = h * 60 + m }) }} /><select aria-label={`时间点 ${i + 1} 场景`} value={point.SceneId} onChange={e => change(s => { s.Schedule[i].SceneId = e.target.value })}>{draft.Scenes.map(scene => <option key={scene.Id} value={scene.Id}>{scene.Name}</option>)}</select><input aria-label={`时间点 ${i + 1} 渐变分钟`} type="number" min={0} max={1440} value={point.TransitionMinutes} onChange={e => change(s => { s.Schedule[i].TransitionMinutes = e.target.valueAsNumber || 0 })} /><span className="caption">分钟</span><Button aria-label={`删除时间点 ${i + 1}`} variant="ghost" size="icon" onClick={() => change(s => { s.Schedule.splice(i, 1) })}><Trash2 /></Button></div>)}
      <div className="actions"><Button variant="outline" disabled={!draft.Scenes.length} onClick={() => change(s => { s.Schedule.push({ MinuteOfDay: 1080, SceneId: s.Scenes[0].Id, TransitionMinutes: 30 }) })}><Plus />添加时间点</Button><Button variant="ghost" onClick={() => void send({ Action: 'resume-plan' })}><RotateCcw />恢复计划</Button></div>
    </>}
    {page === '场景' && <>
      {draft.Scenes.map((scene, i) => <section className="scene-editor" key={scene.Id}><input aria-label={`场景 ${i + 1} 名称`} value={scene.Name} onChange={e => change(s => { s.Scenes[i].Name = e.target.value })} />{snapshot.Displays.map(display => { const target = scene.Targets[display.Id] ?? { Brightness: display.Brightness, Kelvin: display.Kelvin }; return <div key={display.Id}><DisplayRow display={{ ...display, Brightness: target.Brightness }} onCommit={Brightness => change(s => { s.Scenes[i].Targets[display.Id] = { ...target, Brightness } })} />{display.CanAdjustColor && <div className="flex items-center gap-4 pl-11"><span className="caption">色温</span><Level label={`${scene.Name} ${display.Name} 色温`} value={target.Kelvin ?? 6500} min={2700} max={6500} step={100} temperature onCommit={Kelvin => change(s => { s.Scenes[i].Targets[display.Id] = { ...target, Kelvin } })} /><span className="caption whitespace-nowrap">{target.Kelvin ?? '原色'}{target.Kelvin ? ' K' : ''}</span></div>}</div> })}</section>)}
      <Button className="mt-5" variant="outline" onClick={() => change(s => { s.Scenes.push({ Id: crypto.randomUUID(), Name: '自定义', Targets: Object.fromEntries(snapshot.Displays.map(d => [d.Id, { Brightness: d.Brightness, Kelvin: d.Kelvin }])) }) })}><Plus />保存当前亮度为新场景</Button>
    </>}
    {page === '专注' && <><div className="form-row"><label htmlFor="focus">离开副屏后自动变暗</label><Switch id="focus" checked={draft.FocusEnabled} onCheckedChange={on => change(s => { s.FocusEnabled = on })} /></div><div className="form-row"><label htmlFor="delay">等待时间（秒）</label><input id="delay" type="number" min={0} max={600} value={draft.FocusDelaySeconds} onChange={e => change(s => { s.FocusDelaySeconds = e.target.valueAsNumber || 0 })} /></div><div className="form-row"><label htmlFor="dim">专注时硬件亮度（%）</label><input id="dim" type="number" min={0} max={100} value={draft.FocusBrightness} onChange={e => change(s => { s.FocusBrightness = e.target.valueAsNumber || 0 })} /></div><p className="caption mt-5">鼠标返回恢复正常亮度。可从控制面板或托盘暂停、恢复专注。</p></>}
    {page === '通用' && <><div className="form-row"><label htmlFor="startup">登录后自动启动</label><Switch id="startup" checked={draft.LaunchOnStartup} onCheckedChange={on => change(s => { s.LaunchOnStartup = on })} /></div><p className="caption my-5">登录后在托盘启动，不弹出面板。</p><Button variant="outline" onClick={() => void send({ Action: 'rescan' })}><RefreshCw />重新检测显示器</Button></>}
    <div className="actions"><Button disabled={saving} onClick={() => void save()}>{saving ? '正在保存…' : '保存'}</Button><Button variant="ghost" disabled={saving} onClick={() => { setDraft(structuredClone(snapshot.Settings)); setMessage('已重新载入，未保存改动已放弃') }}>放弃改动并重新载入</Button><p className="caption self-center" role="status">{message}</p></div>
  </>
}
function ScheduleChart({ settings, snapshot }: { settings: Settings; snapshot: Snapshot }) {
  const display = snapshot.Displays.find(d => d.CanAdjustColor) ?? snapshot.Displays[0]
  const points = [...settings.Schedule].sort((a, b) => a.MinuteOfDay - b.MinuteOfDay)
  const valid = display && points.length && new Set(points.map(p => p.MinuteOfDay)).size === points.length && points.every((p, i) => settings.Scenes.some(s => s.Id === p.SceneId) && p.TransitionMinutes >= 0 && p.TransitionMinutes <= ((points[(i + 1) % points.length].MinuteOfDay - p.MinuteOfDay + 1440) % 1440 || 1440))
  const brightness: string[] = [], color: string[] = []
  if (valid) for (let minute = 0; minute <= 1440; minute += 2) {
    const time = minute % 1440
    let index = points.findLastIndex(p => p.MinuteOfDay <= time)
    if (index < 0) index = points.length - 1
    const point = points[index], prior = points[(index + points.length - 1) % points.length]
    const target = settings.Scenes.find(s => s.Id === point.SceneId)?.Targets[display.Id]
    const from = settings.Scenes.find(s => s.Id === prior.SceneId)?.Targets[display.Id] ?? target
    if (!target || !from) continue
    const elapsed = (time - point.MinuteOfDay + 1440) % 1440
    const t = point.TransitionMinutes === 0 ? 1 : Math.min(1, elapsed / point.TransitionMinutes)
    const x = 44 + minute / 1440 * 540
    brightness.push(`${x},${170 - (from.Brightness + (target.Brightness - from.Brightness) * t) * 1.3}`)
    if (display.CanAdjustColor && target.Kelvin !== null) { const k = from.Kelvin === null ? target.Kelvin : from.Kelvin + (target.Kelvin - from.Kelvin) * t; color.push(`${x},${170 - (k - 2700) / 3800 * 130}`) }
  }
  return <><p className="caption mt-5">{display?.Name ?? '显示器'} · 计划预览 · 蓝色亮度 / 橙色色温</p><svg className="chart" viewBox="0 0 640 216" role="img" aria-label="当前编辑计划的亮度和色温曲线">{[0, 50, 100].map(v => <g key={v}><line x1="44" x2="584" y1={170 - v * 1.3} y2={170 - v * 1.3} stroke="var(--color-border)" /><text x="0" y={175 - v * 1.3}>{v}%</text></g>)}{[0, 6, 12, 18, 24].map(h => <text key={h} x={28 + h / 24 * 540} y="202">{String(h).padStart(2, '0')}:00</text>)}<text x="590" y="45">6500</text><text x="590" y="175">2700</text>{valid ? <><polyline points={brightness.join(' ')} fill="none" stroke="var(--color-primary)" strokeWidth="2" /><polyline points={color.join(' ')} fill="none" stroke="var(--warm)" strokeWidth="2" /></> : <text x="170" y="110">{points.length ? '请检查时间点和渐变时长' : '添加时间点，预览一天的变化'}</text>}</svg></>
}
