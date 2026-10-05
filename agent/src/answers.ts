/**
 * Answers application questions from the user's saved answers. No AI: a question is matched by
 * keywords against rules the user wrote. When nothing matches, the answer is undefined and the
 * caller must leave the field alone — a missing fact is never guessed.
 */

export interface AnswerRule {
  /** Any of these phrases appearing in the question selects this rule (case-insensitive). */
  match: string[]
  value: string
}

export interface Answers {
  /** Checked in order; the first matching rule wins. */
  fields: AnswerRule[]
  /** "How many years of experience do you have with X?" — keys are skills as they appear in questions. */
  skills: Record<string, number>
  defaults: {
    /** Used for experience questions that name no known skill. null = leave them for you ("Needs you"). */
    yearsOfExperience: number | null
    /** Used for Yes/No questions no rule covers. Leave empty to treat them as unanswered. */
    yesNo: 'Yes' | 'No' | ''
  }
}

export type FieldKind = 'text' | 'number' | 'textarea' | 'select' | 'radio' | 'checkbox' | 'file'

export interface Question {
  label: string
  kind: FieldKind
  options?: string[]
}

export function normalize(text: string): string {
  return text
    .toLowerCase()
    .replace(/[*?:]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

const EXPERIENCE = /\b(years?|yrs)\b.*\b(experience|worked|working)\b|\bexperience\b.*\b(years?|yrs)\b/

function isYesNo(options: string[] | undefined): boolean {
  if (!options || options.length < 2) return false
  const set = new Set(options.map((o) => normalize(o)).filter((o) => o && !o.startsWith('select')))
  return set.size === 2 && set.has('yes') && set.has('no')
}

export function answerFor(q: Question, answers: Answers): string | undefined {
  const label = normalize(q.label)
  if (!label) return undefined

  for (const rule of answers.fields) {
    if (rule.match.some((m) => m.trim() && label.includes(normalize(m)))) return rule.value
  }

  if (EXPERIENCE.test(label)) {
    // Longest skill first so ".net core" wins over ".net".
    const skill = Object.keys(answers.skills)
      .sort((a, b) => b.length - a.length)
      .find((s) => label.includes(normalize(s)))
    if (skill) return String(answers.skills[skill])
    return answers.defaults.yearsOfExperience === null ? undefined : String(answers.defaults.yearsOfExperience)
  }

  if (isYesNo(q.options) && answers.defaults.yesNo) return answers.defaults.yesNo
  return undefined
}

/**
 * Chooses the option that best fits an answer: exact text, then prefix, then containment,
 * then a numeric range ("3-5 years") that contains a numeric answer.
 */
export function pickOption(options: string[], answer: string): string | undefined {
  const a = normalize(answer)
  const usable = options.filter((o) => normalize(o) && !/^select( an)? option$/.test(normalize(o)))
  const exact = usable.find((o) => normalize(o) === a)
  if (exact) return exact
  const prefix = usable.find((o) => normalize(o).startsWith(a))
  if (prefix) return prefix
  const contains = usable.find((o) => normalize(o).includes(a) || (a.length > 2 && a.includes(normalize(o))))
  if (contains) return contains

  const n = Number(a)
  if (!Number.isNaN(n)) {
    for (const o of usable) {
      const range = normalize(o).match(/(\d+(?:\.\d+)?)\s*(?:-|to|–)\s*(\d+(?:\.\d+)?)/)
      if (range && n >= Number(range[1]) && n <= Number(range[2])) return o
      const plus = normalize(o).match(/(\d+(?:\.\d+)?)\s*\+/)
      if (plus && n >= Number(plus[1])) return o
    }
  }
  return undefined
}
