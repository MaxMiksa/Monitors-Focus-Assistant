import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { I18nContext } from './i18n-context'
import { resolveLanguage } from './i18n'
import type { Language } from './i18n'

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(() => {
    const initial = new URLSearchParams(location.search).get('language')
    return initial === 'en' || initial === 'zh-CN' ? initial : 'system'
  })
  const [systemLanguage, setSystemLanguage] = useState(() => new URLSearchParams(location.search).get('systemLanguage') ?? navigator.language)
  useEffect(() => { document.documentElement.lang = resolveLanguage(language, systemLanguage) }, [language, systemLanguage])
  return <I18nContext.Provider value={{ language, setLanguage, systemLanguage, setSystemLanguage }}>{children}</I18nContext.Provider>
}
