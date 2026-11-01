import { pageLabels } from '@/lib/i18n'
import { useI18n } from '@/lib/i18n-context'
import type { Page, Language } from '@/lib/i18n'
import { useState } from 'react'
import { Plus, Trash2, RefreshCw, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { DisplayRow, Level } from '@/App'
import type { Send } from '@/App'
import { captureBehavior } from '@/lib/control'
import type { SceneBehavior, Settings, Snapshot } from '@/lib/control'

export function SettingsEditor({ page, snapshot, send }: { page: Page; snapshot: Snapshot; send: Send }) {
  const { t, sceneName, setLanguage, displayName, message: translateMessage } = useI18n()
  const [draft, setDraft] = useState(() => structuredClone(snapshot.Settings))
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  function change(update: (settings: Settings) => void) { setDraft(current => { const next = structuredClone(current); update(next); return next }); setMessage('') }
  function changeBehavior(index: number, update: (behavior: SceneBehavior) => void) {
    change(s => { const scene = s.Scenes[index]; scene.Behavior ??= captureBehavior(s); update(scene.Behavior) })
  }
  async function save() {
    setSaving(true)
    try { const saved = await send({ Action: 'save', Settings: draft }); if (saved) { setDraft(structuredClone(saved.Settings)); setMessage('已保存') } }
    finally { setSaving(false) }
  }
  const title = page === 'schedule' ? t('让屏幕跟随一天的节奏') : page === 'focus' ? t('留心眼前，余下安静') : t(pageLabels[page])
  return <><h1>{title}</h1>
    {page === 'schedule' && <>
      <div className="form-row"><label htmlFor="schedule">{t('按时间自动渐变')}</label><Switch id="schedule" checked={draft.ScheduleEnabled} onCheckedChange={on => change(s => { s.ScheduleEnabled = on })} /></div>
      <p className="caption mt-4">{t('到达时间点后应用场景规则并柔和渐变。新增节点默认 30 分钟，可设数小时，须在下一节点前完成。')}</p>
      <ScheduleChart settings={draft} snapshot={snapshot} />
      {draft.Schedule.map((point, i) => <div className="plan-row" key={i}><input aria-label={t('时间点 {0}', i + 1)} type="time" required value={`${String(Math.floor(point.MinuteOfDay / 60)).padStart(2, '0')}:${String(point.MinuteOfDay % 60).padStart(2, '0')}`} onChange={e => { if (!e.target.value) return; const [h, m] = e.target.value.split(':').map(Number); change(s => { s.Schedule[i].MinuteOfDay = h * 60 + m }) }} /><select aria-label={t('时间点 {0} 场景', i + 1)} value={point.SceneId} onChange={e => change(s => { s.Schedule[i].SceneId = e.target.value })}>{draft.Scenes.map(scene => <option key={scene.Id} value={scene.Id}>{sceneName(scene)}</option>)}</select><input aria-label={t('时间点 {0} 渐变分钟', i + 1)} type="number" min={0} max={1440} value={point.TransitionMinutes} onChange={e => change(s => { s.Schedule[i].TransitionMinutes = e.target.valueAsNumber || 0 })} /><span className="caption">{t('分钟')}</span><Button aria-label={t('删除时间点 {0}', i + 1)} variant="ghost" size="icon" onClick={() => change(s => { s.Schedule.splice(i, 1) })}><Trash2 /></Button></div>)}
      <div className="actions"><Button variant="outline" disabled={!draft.Scenes.length} onClick={() => change(s => { s.Schedule.push({ MinuteOfDay: 1080, SceneId: s.Scenes[0].Id, TransitionMinutes: 30 }) })}><Plus />{t('添加时间点')}</Button><Button variant="ghost" onClick={() => void send({ Action: 'resume-plan' })}><RotateCcw />{t('恢复计划')}</Button></div>
    </>}
    {page === 'scenes' && <>
      {draft.Scenes.map((scene, i) => {
        const behavior = scene.Behavior ?? captureBehavior(draft)
        return <section className="scene-editor" key={scene.Id} aria-label={t('场景 {0}', i + 1)}>
          <input aria-label={t('场景 {0} 名称', i + 1)} value={sceneName(scene)} onChange={e => change(s => { s.Scenes[i].Name = e.target.value; s.Scenes[i].DefaultName = undefined })} />
          {snapshot.Displays.map(display => { const target = scene.Targets[display.Id] ?? { Brightness: display.Brightness, Kelvin: display.Kelvin }; return <div key={display.Id}><DisplayRow display={{ ...display, Brightness: target.Brightness, Error: null, CanAdjust: true }} onCommit={Brightness => change(s => { s.Scenes[i].Targets[display.Id] = { ...target, Brightness } })} />{display.CanAdjustColor && <div className="flex items-center gap-4 pl-11"><span className="caption whitespace-nowrap">{t('色温')}</span><Level label={t('{0} {1} 色温', sceneName(scene), displayName(display.Name))} value={target.Kelvin ?? 6500} min={2700} max={6500} step={100} temperature onCommit={Kelvin => change(s => { s.Scenes[i].Targets[display.Id] = { ...target, Kelvin } })} /><span className="caption whitespace-nowrap">{target.Kelvin ?? t('原色')}{target.Kelvin ? ' K' : ''}</span><Button variant="ghost" size="icon" aria-label={t('{0}恢复原色', displayName(display.Name))} disabled={target.Kelvin === null} onClick={() => change(s => { s.Scenes[i].Targets[display.Id] = { ...target, Kelvin: null } })}><RotateCcw /></Button></div>}</div> })}
          <div className="form-row"><label htmlFor={`${scene.Id}-link`}>{t('保持亮度比例')}</label><Switch id={`${scene.Id}-link`} checked={behavior.Linked} disabled={!behavior.Linked && snapshot.Displays.filter(d => d.CanAdjust).length < 2} onCheckedChange={on => changeBehavior(i, b => {
            b.Linked = on
            if (on && Object.keys(b.LinkWeights).length < 2) b.LinkWeights = Object.fromEntries(snapshot.Displays.filter(d => d.CanAdjust).map(d => [d.Id, scene.Targets[d.Id]?.Brightness ?? d.Brightness]))
          })} /></div>
          {behavior.Linked && Object.entries(behavior.LinkWeights).map(([id, weight]) => <div className="form-row" key={id}><label htmlFor={`${scene.Id}-weight-${id}`}>{t('{0} · 比例权重', displayName(snapshot.Displays.find(d => d.Id === id)?.Name ?? t('{0}（离线）', id)))}</label><input id={`${scene.Id}-weight-${id}`} type="number" min={0.01} step="any" value={weight} onChange={e => changeBehavior(i, b => { b.LinkWeights[id] = e.target.valueAsNumber || 0 })} /></div>)}
          <div className="form-row"><label htmlFor={`${scene.Id}-focus`}>{t('启用专注调暗')}</label><Switch id={`${scene.Id}-focus`} checked={behavior.FocusEnabled} onCheckedChange={on => changeBehavior(i, b => { b.FocusEnabled = on })} /></div>
          <ControlledDisplays id={scene.Id} snapshot={snapshot} value={behavior.ControlledDisplayIds} onChange={ids => changeBehavior(i, b => { b.ControlledDisplayIds = ids })} />
          <div className="actions"><Button variant="ghost" disabled={draft.Schedule.some(p => p.SceneId === scene.Id)} onClick={() => change(s => { s.Scenes.splice(i, 1) })}><Trash2 />{t('删除场景')}</Button>{draft.Schedule.some(p => p.SceneId === scene.Id) && <p className="caption self-center">{t('先移除时间计划中的引用，再删除场景。')}</p>}</div>
        </section>
      })}
      <Button className="mt-5" variant="outline" onClick={() => change(s => { s.Scenes.push({ Id: crypto.randomUUID(), Name: '自定义', DefaultName: '自定义', Behavior: captureBehavior(snapshot.Settings), Targets: Object.fromEntries(snapshot.Displays.map(d => [d.Id, { Brightness: d.Brightness, Kelvin: d.Kelvin }])) }) })}><Plus />{t('保存当前设置为新场景')}</Button>
    </>}
    {page === 'focus' && <>
      <div className="form-row"><label htmlFor="focus">{t('离开副屏后自动变暗')}</label><Switch id="focus" checked={draft.FocusEnabled} onCheckedChange={on => change(s => { s.FocusEnabled = on })} /></div>
      <ControlledDisplays id="current" snapshot={snapshot} value={draft.ControlledDisplayIds ?? null} onChange={ids => change(s => { s.ControlledDisplayIds = ids })} />
      <div className="form-row"><label htmlFor="fullscreen">{t('副屏全屏或最大化时保持亮屏')}</label><Switch id="fullscreen" checked={draft.KeepSecondaryAwakeOnFullscreen} onCheckedChange={on => change(s => { s.KeepSecondaryAwakeOnFullscreen = on })} /></div>
      <p className="caption mt-4">{t('副屏上有全屏或最大化窗口时恢复亮度，切回主屏也保持亮着。窗口还原、最小化或移走后重新计时。')}</p>
      <div className="form-row"><label htmlFor="delay">{t('等待时间（秒）')}</label><input id="delay" type="number" min={0} max={600} value={draft.FocusDelaySeconds} onChange={e => change(s => { s.FocusDelaySeconds = e.target.valueAsNumber || 0 })} /></div>
      <div className="form-row"><label htmlFor="dim">{t('专注时硬件亮度（%）')}</label><input id="dim" type="number" min={0} max={100} value={draft.FocusBrightness} onChange={e => change(s => { s.FocusBrightness = e.target.valueAsNumber || 0 })} /></div>
      <p className="caption mt-5">{t('鼠标返回恢复正常亮度。可从控制面板或托盘暂停、恢复专注。')}</p>
    </>}
    {page === 'general' && <><div className="form-row"><label htmlFor="language">{t('语言')}</label><select id="language" value={draft.Language ?? 'system'} onChange={e => { const language = e.target.value as Language; change(s => { s.Language = language }); setLanguage(language) }}><option value="system">{t('跟随系统')}</option><option value="zh-CN">简体中文</option><option value="en">English</option></select></div><div className="form-row"><label htmlFor="startup">{t('登录后自动启动')}</label><Switch id="startup" checked={draft.LaunchOnStartup} onCheckedChange={on => change(s => { s.LaunchOnStartup = on })} /></div><p className="caption my-5">{t('登录后在托盘启动，不弹出面板。')}</p><Button variant="outline" onClick={() => void send({ Action: 'rescan' })}><RefreshCw />{t('重新检测显示器')}</Button></>}
    <div className="actions"><Button disabled={saving} onClick={() => void save()}>{saving ? t('正在保存…') : t('保存')}</Button><Button variant="ghost" disabled={saving} onClick={() => { setDraft(structuredClone(snapshot.Settings)); setLanguage(snapshot.Settings.Language ?? 'system'); setMessage('已重新载入，未保存改动已放弃') }}>{t('放弃改动并重新载入')}</Button><p className="caption self-center" role="status">{translateMessage(message)}</p></div>
  </>
}
function ControlledDisplays({ id, snapshot, value, onChange }: { id: string; snapshot: Snapshot; value: string[] | null; onChange: (ids: string[] | null) => void }) {
  const { t, displayName } = useI18n()
  const displays = snapshot.Displays.filter(d => !d.IsPrimary)
  const offline = (value ?? []).filter(key => !snapshot.Displays.some(d => d.Id === key))
  return <>
    <div className="form-row"><label htmlFor={`${id}-controlled`}>{t('参与专注的副屏')}</label><select id={`${id}-controlled`} value={value === null ? 'all' : 'selected'} onChange={e => onChange(e.target.value === 'all' ? null : displays.map(d => d.Id))}><option value="all">{t('全部副屏')}</option><option value="selected">{t('指定副屏')}</option></select></div>
    {value !== null && [...displays.map(d => ({ Id: d.Id, Name: displayName(d.Name) })), ...offline.map(Id => ({ Id, Name: t('{0}（离线）', Id) }))].map(d => <div className="form-row" key={d.Id}><label htmlFor={`${id}-controlled-${d.Id}`}>{d.Name}</label><Switch id={`${id}-controlled-${d.Id}`} checked={value.includes(d.Id)} onCheckedChange={on => onChange(on ? [...value, d.Id] : value.filter(key => key !== d.Id))} /></div>)}
    <p className="caption mt-4">{t('主屏始终保持亮度。指定副屏全部关闭时，不调暗任何屏幕。')}</p>
  </>
}
function ScheduleChart({ settings, snapshot }: { settings: Settings; snapshot: Snapshot }) {
  const { t, displayName } = useI18n()
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
    const progress = point.TransitionMinutes === 0 ? 1 : Math.min(1, elapsed / point.TransitionMinutes)
    const t = progress * progress * (3 - 2 * progress)
    const x = 44 + minute / 1440 * 540
    brightness.push(`${x},${170 - (from.Brightness + (target.Brightness - from.Brightness) * t) * 1.3}`)
    if (display.CanAdjustColor) { const k = (from.Kelvin ?? 6500) + ((target.Kelvin ?? 6500) - (from.Kelvin ?? 6500)) * t; color.push(`${x},${170 - (k - 2700) / 3800 * 130}`) }
  }
  return <><p className="caption mt-5">{t('{0} · 计划预览 · 蓝色亮度 / 橙色色温', display ? displayName(display.Name) : t('显示器'))}</p><svg className="chart" viewBox="0 0 640 216" role="img" aria-label={t('当前编辑计划的亮度和色温曲线')}>{[0, 50, 100].map(v => <g key={v}><line x1="44" x2="584" y1={170 - v * 1.3} y2={170 - v * 1.3} stroke="var(--color-border)" /><text x="0" y={175 - v * 1.3}>{v}%</text></g>)}{[0, 6, 12, 18, 24].map(h => <text key={h} x={28 + h / 24 * 540} y="202">{String(h).padStart(2, '0')}:00</text>)}<text x="590" y="45">6500</text><text x="590" y="175">2700</text>{valid ? <><polyline points={brightness.join(' ')} fill="none" stroke="var(--color-primary)" strokeWidth="2" /><polyline points={color.join(' ')} fill="none" stroke="var(--warm)" strokeWidth="2" /></> : <text x="170" y="110">{points.length ? t('请检查时间点和渐变时长') : t('添加时间点，预览一天的变化')}</text>}</svg></>
}
