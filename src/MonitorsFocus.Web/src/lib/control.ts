export interface Display { Id: string; Name: string; Brightness: number; CanAdjust: boolean; CanAdjustColor: boolean; Kelvin: number | null; Error: string | null; IsPrimary: boolean }
export interface Target { Brightness: number; Kelvin: number | null }
export interface SceneBehavior { Linked: boolean; LinkWeights: Record<string, number>; FocusEnabled: boolean; ControlledDisplayIds: string[] | null }
export interface Scene { Id: string; Name: string; Targets: Record<string, Target>; Behavior: SceneBehavior }
export interface PlanPoint { MinuteOfDay: number; SceneId: string; TransitionMinutes: number }
export interface Settings extends SceneBehavior { SchemaVersion: number; Revision: number; FocusDelaySeconds: number; FocusBrightness: number; ScheduleEnabled: boolean; LaunchOnStartup: boolean; Scenes: Scene[]; Schedule: PlanPoint[] }
export interface Snapshot { Displays: Display[]; Settings: Settings; FocusPaused: boolean; Source: string; Error: string | null; Warning?: string | null }
export interface Request { Action: string; Id?: string; Value?: number; Settings?: Settings }
export function captureBehavior(settings: Settings): SceneBehavior {
  return structuredClone({ Linked: settings.Linked, LinkWeights: settings.LinkWeights,
    FocusEnabled: settings.FocusEnabled, ControlledDisplayIds: settings.ControlledDisplayIds ?? null })
}
type Reply = { Id: string; Snapshot?: Snapshot; Error?: string }
declare global { interface Window { chrome?: { webview?: { postMessage(message: unknown): void; addEventListener(type: 'message', listener: (event: MessageEvent<Reply>) => void): void } } } }
const pending = new Map<string, { resolve: (value: Snapshot) => void; reject: (error: Error) => void; timer: ReturnType<typeof setTimeout> }>()
window.chrome?.webview?.addEventListener('message', ({ data }) => {
  const task = pending.get(data.Id)
  if (!task) return
  clearTimeout(task.timer); pending.delete(data.Id)
  if (data.Error) task.reject(new Error(data.Error))
  else if (data.Snapshot) task.resolve(data.Snapshot)
  else task.reject(new Error('控制核心未返回状态'))
})
export const isDesktop = !!window.chrome?.webview
export function request(Request: Request): Promise<Snapshot> {
  if (!isDesktop) return Promise.reject(new Error('浏览器预览未连接硬件。请从桌面控制面板打开。'))
  const Id = crypto.randomUUID()
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => { pending.delete(Id); reject(new Error('控制核心响应超时，请重试')) }, 12000)
    pending.set(Id, { resolve, reject, timer })
    window.chrome!.webview!.postMessage({ Id, Request })
  })
}
export function resizePanel(settings: boolean) {
  window.chrome?.webview?.postMessage({ Id: crypto.randomUUID(), Request: { Action: 'window', Value: settings ? 1 : 0 } })
}
// Explicit preview only; never substitute demo data when hardware requests fail.
export const preview: Snapshot = {
  Displays: [
    { Id: 'preview-external', Name: 'AOC U27G4', Brightness: 40, CanAdjust: true, CanAdjustColor: true, Kelvin: 4500, Error: null, IsPrimary: true },
    { Id: 'preview-internal', Name: '笔记本屏幕', Brightness: 60, CanAdjust: true, CanAdjustColor: false, Kelvin: null, Error: null, IsPrimary: false },
  ],
  Settings: { SchemaVersion: 1, Revision: 0, Linked: false, LinkWeights: {}, FocusEnabled: false, ControlledDisplayIds: null, FocusDelaySeconds: 180, FocusBrightness: 10, ScheduleEnabled: false, LaunchOnStartup: false,
    Scenes: ['日常', '夜间', '专注'].map((Name, i) => ({ Id: `preview-${i}`, Name, Behavior: { Linked: false, LinkWeights: {}, FocusEnabled: i === 2, ControlledDisplayIds: null }, Targets: { 'preview-external': { Brightness: [40, 20, 30][i], Kelvin: [6500, 3200, 4500][i] }, 'preview-internal': { Brightness: [60, 30, 45][i], Kelvin: null } } })), Schedule: [] },
  FocusPaused: false, Source: '视觉预览 · 未连接硬件', Error: null,
}
