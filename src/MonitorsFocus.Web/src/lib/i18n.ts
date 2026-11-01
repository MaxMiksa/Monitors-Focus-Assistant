import strings from '../../../MonitorsFocus.Core/strings.json'
import type { Scene } from './control'

export type Language = 'system' | 'zh-CN' | 'en'
export type TextKey = keyof typeof strings
export type Page = 'displays' | 'scenes' | 'schedule' | 'focus' | 'general'
export const pageLabels: Record<Page, TextKey> = { displays: '显示器', scenes: '场景', schedule: '时间计划', focus: '专注', general: '通用' }
export function resolveLanguage(language: Language, system = navigator.language): 'zh-CN' | 'en' {
  return language === 'system' ? (system.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en') : language
}
export function createTranslator(language: Language, system = navigator.language) {
  function text(key: TextKey, ...args: (string | number)[]) {
    const template = resolveLanguage(language, system) === 'en' ? strings[key] : key
    return template.replace(/\{(\d+)\}/g, (_, index: string) => String(args[Number(index)] ?? ''))
  }
  const errorTemplates: TextKey[] = ['连接失败：{0}', '原配置未覆盖：{0}', '检测失败：{0}', '显示器恢复失败：{0}', '恢复失败：{0}', 'WMI 亮度调节失败：{0}', '时间计划未应用：{0}']
  const reverse = new Map(Object.entries(strings).map(([key, value]) => [value, key as TextKey]))
  function message(value: string): string {
    if (Object.hasOwn(strings, value)) return text(value as TextKey)
    const key = reverse.get(value)
    if (key) return text(key)
    for (const template of errorTemplates) for (const source of [template, strings[template]]) {
      const prefix = source.slice(0, source.indexOf('{0}'))
      if (value.startsWith(prefix)) return text(template, message(value.slice(prefix.length)))
    }
    return value
  }
  function sceneName(scene: Scene) {
    return scene.DefaultName && scene.Name === scene.DefaultName && Object.hasOwn(strings, scene.DefaultName)
      ? text(scene.DefaultName as TextKey) : scene.Name
  }
  function displayName(name: string) { return name === '笔记本屏幕' || name === 'Laptop display' ? text('笔记本屏幕') : name }
  return { t: text, message, sceneName, displayName }
}
