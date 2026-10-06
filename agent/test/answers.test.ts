import { describe, expect, it } from 'vitest'
import { answerFor, pickOption, type Answers } from '../src/answers.ts'

const answers: Answers = {
  fields: [
    { match: ['notice period'], value: '30' },
    { match: ['expected ctc', 'expected salary'], value: '1800000' },
    { match: ['sponsorship'], value: 'No' },
  ],
  skills: { 'c#': 6, '.net': 5, '.net core': 4, react: 3 },
  defaults: { yearsOfExperience: 5, yesNo: 'Yes' },
}

describe('answerFor', () => {
  it('uses only the approved cover note for cover-letter-like free-text fields', () => {
    expect(answerFor({ label: 'Cover letter', kind: 'textarea' }, answers, 'Approved exact text')).toBe('Approved exact text')
    expect(answerFor({ label: 'Why do you want to join us?', kind: 'textarea' }, answers, 'Approved exact text')).toBe('Approved exact text')
    expect(answerFor({ label: 'Tell us about yourself', kind: 'textarea' }, answers, 'Approved exact text')).toBeUndefined()
    expect(answerFor({ label: 'Cover letter', kind: 'textarea' }, answers)).toBeUndefined()
  })

  it('uses the first matching rule, ignoring case and punctuation', () => {
    expect(answerFor({ label: 'What is your Notice Period?*', kind: 'text' }, answers)).toBe('30')
    expect(answerFor({ label: 'Expected CTC (in INR)', kind: 'number' }, answers)).toBe('1800000')
  })

  it('answers experience questions per skill, longest skill name first', () => {
    expect(answerFor({ label: 'How many years of work experience do you have with C#?', kind: 'text' }, answers)).toBe('6')
    expect(answerFor({ label: 'Years of experience in .NET Core', kind: 'text' }, answers)).toBe('4')
    expect(answerFor({ label: 'How many years of experience do you have with Kubernetes?', kind: 'text' }, answers)).toBe('5')
  })

  it('falls back to the default only for genuine yes/no questions', () => {
    expect(answerFor({ label: 'Are you comfortable with shifts?', kind: 'radio', options: ['Yes', 'No'] }, answers)).toBe('Yes')
    expect(answerFor({ label: 'Will you require sponsorship?', kind: 'radio', options: ['Yes', 'No'] }, answers)).toBe('No')
    expect(answerFor({ label: 'Pick your shift', kind: 'radio', options: ['Day', 'Night'] }, answers)).toBeUndefined()
  })

  it('never guesses free-text questions no rule covers', () => {
    expect(answerFor({ label: 'Why do you want to join us?', kind: 'textarea' }, answers)).toBeUndefined()
    expect(answerFor({ label: '', kind: 'text' }, answers)).toBeUndefined()
  })

  it('leaves yes/no questions unanswered when the user turned the default off', () => {
    const strict = { ...answers, defaults: { ...answers.defaults, yesNo: '' as const } }
    expect(answerFor({ label: 'Are you comfortable with shifts?', kind: 'radio', options: ['Yes', 'No'] }, strict)).toBeUndefined()
  })
})

describe('pickOption', () => {
  it('prefers exact, then prefix, then containment', () => {
    expect(pickOption(['Select an option', 'Yes', 'No'], 'yes')).toBe('Yes')
    expect(pickOption(['Immediately', '15 days', '30 days or less'], '30')).toBe('30 days or less')
    expect(pickOption(['I prefer not to say', 'Male', 'Female'], 'Prefer not to say')).toBe('I prefer not to say')
  })

  it('maps a number into a range option', () => {
    expect(pickOption(['0-2 years', '3-5 years', '6+ years'], '4')).toBe('3-5 years')
    expect(pickOption(['0-2 years', '3-5 years', '6+ years'], '9')).toBe('6+ years')
  })

  it('returns undefined rather than a wrong option', () => {
    expect(pickOption(['Select an option', 'Day', 'Night'], 'Yes')).toBeUndefined()
  })
})

describe('nothing assumed by default', () => {
  const none: Answers = { fields: [], skills: { 'c#': 6 }, defaults: { yearsOfExperience: null, yesNo: '' } }

  it('leaves experience in an unlisted skill and uncovered yes/no questions to the user', () => {
    expect(answerFor({ label: 'How many years of experience do you have with Kubernetes?', kind: 'text' }, none)).toBeUndefined()
    expect(answerFor({ label: 'Are you comfortable with night shifts?', kind: 'radio', options: ['Yes', 'No'] }, none)).toBeUndefined()
    expect(answerFor({ label: 'How many years of experience do you have with C#?', kind: 'text' }, none)).toBe('6')
  })
})
