import { createContext, useContext, useMemo } from 'react'
import { createTranslator } from './i18n'
import type { Language } from './i18n'

export const I18nContext = createContext<{ language: Language; setLanguage: (value: Language) => void;
  systemLanguage: string; setSystemLanguage: (value: string) => void } | null>(null)
export function useI18n() {
  const context = useContext(I18nContext)
  if (!context) throw new Error('I18nProvider missing')
  const translator = useMemo(() => createTranslator(context.language, context.systemLanguage), [context.language, context.systemLanguage])
  return { ...context, ...translator }
}
