import type { Locator, Page } from 'playwright'
import { answerFor, pickOption, type Answers, type FieldKind } from './answers.ts'

export interface FieldInfo {
  id: string
  kind: FieldKind
  label: string
  required: boolean
  filled: boolean
  /** Radio/select option texts, in page order. */
  options: string[]
  combobox: boolean
}

export interface FillResult {
  answered: string[]
  /** Required fields left empty because no saved answer covers them. */
  unanswered: string[]
}

/**
 * Finds the fields inside one form step and tags them with data-op-field so they can be filled.
 * Labels are resolved the way assistive tech does (label[for], aria-label, aria-labelledby,
 * wrapping label, group legend), which keeps this independent of any one site's class names.
 */
export async function discoverFields(root: Locator): Promise<FieldInfo[]> {
  return root.evaluate((rootEl) => {
    const text = (e: Element | null | undefined) => ((e as HTMLElement | null)?.innerText ?? e?.textContent ?? '').replace(/\s+/g, ' ').trim()
    const visible = (e: Element | null) => {
      if (!e) return false
      const s = getComputedStyle(e)
      const r = e.getBoundingClientRect()
      return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0 && r.height > 0
    }
    const labelElFor = (el: Element) => (el.id ? rootEl.querySelector(`label[for="${CSS.escape(el.id)}"]`) : null)
    const labelFor = (el: Element): string => {
      const l = labelElFor(el)
      if (l && text(l)) return text(l)
      const aria = el.getAttribute('aria-label')
      if (aria) return aria
      const by = el.getAttribute('aria-labelledby')
      if (by) {
        const t = by.split(/\s+/).map((id) => text(document.getElementById(id))).join(' ').trim()
        if (t) return t
      }
      const wrap = el.closest('label')
      if (wrap && text(wrap)) return text(wrap)
      const group = el.closest('fieldset, [data-test-form-element], .fb-dash-form-element, .form-group, .field')
      const gl = group?.querySelector('legend, label, [class*="label"]')
      if (gl && text(gl)) return text(gl)
      return el.getAttribute('placeholder') ?? el.getAttribute('name') ?? ''
    }
    const isRequired = (el: Element, label: string, group?: Element | null) =>
      (el as HTMLInputElement).required ||
      el.getAttribute('aria-required') === 'true' ||
      group?.getAttribute('aria-required') === 'true' ||
      /\*\s*$|\brequired\b/i.test(label)

    const fields: {
      id: string
      kind: string
      label: string
      required: boolean
      filled: boolean
      options: string[]
      combobox: boolean
    }[] = []
    const radioGroups = new Map<string, HTMLInputElement[]>()
    let n = 0

    for (const el of Array.from(rootEl.querySelectorAll('input, select, textarea'))) {
      const input = el as HTMLInputElement
      const type = (input.getAttribute('type') ?? (el.tagName === 'SELECT' ? 'select' : el.tagName === 'TEXTAREA' ? 'textarea' : 'text')).toLowerCase()
      if (['hidden', 'submit', 'button', 'image', 'reset', 'search'].includes(type) || input.disabled) continue

      if (type === 'radio') {
        const key = input.name || input.closest('fieldset')?.id || `radio-${n}`
        radioGroups.set(key, [...(radioGroups.get(key) ?? []), input])
        continue
      }
      // Hidden native inputs are common for styled checkboxes and file pickers; their label is what shows.
      if (!visible(el) && !(['checkbox', 'file'].includes(type) && visible(labelElFor(el)))) {
        if (type !== 'file') continue
      }

      const id = `f${n++}`
      el.setAttribute('data-op-field', id)
      const label = labelFor(el)
      const kind = type === 'select' ? 'select' : type === 'textarea' ? 'textarea' : type === 'checkbox' ? 'checkbox' : type === 'file' ? 'file' : type === 'number' ? 'number' : 'text'
      const select = el as HTMLSelectElement
      const options = kind === 'select' ? Array.from(select.options).map((o) => o.text.trim()) : []
      const filled =
        kind === 'select'
          ? select.selectedIndex > 0 && !/^select/i.test(select.options[select.selectedIndex]?.text.trim() ?? '')
          : kind === 'checkbox'
            ? input.checked
            : kind === 'file'
              ? (input.files?.length ?? 0) > 0
              : input.value.trim() !== ''
      fields.push({
        id,
        kind,
        label,
        required: isRequired(el, label),
        filled,
        options,
        combobox: input.getAttribute('role') === 'combobox' || input.hasAttribute('aria-autocomplete'),
      })
    }

    for (const radios of radioGroups.values()) {
      const id = `f${n++}`
      const group = radios[0].closest('fieldset, [role="radiogroup"]')
      const legend = group?.querySelector('legend, [class*="label"]:not(label)')
      const label = (legend && text(legend)) || group?.getAttribute('aria-label') || labelFor(radios[0])
      const options = radios.map((r, i) => {
        r.setAttribute('data-op-field', id)
        r.setAttribute('data-op-option', String(i))
        return text(labelElFor(r)) || r.value
      })
      if (!radios.some((r) => visible(r) || visible(labelElFor(r)))) continue
      fields.push({
        id,
        kind: 'radio',
        label,
        required: radios.some((r) => isRequired(r, label, group)),
        filled: radios.some((r) => r.checked),
        options,
        combobox: false,
      })
    }
    return fields
  }) as Promise<FieldInfo[]>
}

const AGREE = /\b(agree|terms|acknowledge|consent|certify|confirm that)\b/i

/** Fills every empty field a saved answer covers. Prefilled values are left as the user (or site) set them. */
export async function fillStep(page: Page, root: Locator, answers: Answers, opts: { followCompanies: boolean }): Promise<FillResult> {
  const result: FillResult = { answered: [], unanswered: [] }
  for (const f of await discoverFields(root)) {
    const el = root.locator(`[data-op-field="${f.id}"]`)

    if (f.kind === 'checkbox') {
      const wanted = /follow/i.test(f.label) ? opts.followCompanies : f.required || AGREE.test(f.label) ? true : undefined
      if (wanted !== undefined && wanted !== f.filled) {
        await el.setChecked(wanted, { force: true })
        result.answered.push(f.label)
      }
      continue
    }
    if (f.filled) continue
    if (f.kind === 'file') {
      if (f.required) result.unanswered.push(f.label || 'File upload')
      continue
    }

    const answer = answerFor({ label: f.label, kind: f.kind, options: f.options }, answers)

    if (f.kind === 'select') {
      const option = answer ? pickOption(f.options, answer) : undefined
      if (option) {
        await el.selectOption({ label: option })
        result.answered.push(f.label)
      } else if (f.required) result.unanswered.push(f.label)
      continue
    }

    if (f.kind === 'radio') {
      const option = answer ? pickOption(f.options, answer) : undefined
      if (option) {
        const radio = root.locator(`[data-op-field="${f.id}"][data-op-option="${f.options.indexOf(option)}"]`)
        const id = await radio.getAttribute('id')
        const label = id ? root.locator(`label[for="${id}"]`) : null
        // Styled radios hide the input; clicking the visible label is what a person does.
        if (label && (await label.count()) > 0 && (await label.first().isVisible())) await label.first().click()
        else await radio.check({ force: true })
        result.answered.push(f.label)
      } else if (f.required) result.unanswered.push(f.label)
      continue
    }

    if (answer === undefined) {
      if (f.required) result.unanswered.push(f.label)
      continue
    }
    await el.fill(answer)
    if (f.combobox) {
      // Typeahead fields (e.g. city) only accept a value picked from their suggestion list.
      const option = page.locator('[role="listbox"] [role="option"]').first()
      try {
        await option.waitFor({ state: 'visible', timeout: 2500 })
        await option.click()
      } catch {
        /* no suggestions shown; keep the typed value */
      }
    }
    result.answered.push(f.label)
  }
  return result
}

/** Visible validation messages inside a form step. */
export async function validationErrors(root: Locator): Promise<string[]> {
  const texts = await root
    .locator('.artdeco-inline-feedback--error, [role="alert"], .error-message, .field-error')
    .evaluateAll((els) =>
      els
        .filter((e) => (e as HTMLElement).offsetParent !== null)
        .map((e) => (e as HTMLElement).innerText.replace(/\s+/g, ' ').trim())
        .filter(Boolean),
    )
  return [...new Set(texts)]
}
