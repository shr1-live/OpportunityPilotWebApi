export const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

export function between(min: number, max: number): number {
  return Math.round(min + Math.random() * Math.max(0, max - min))
}

/** Thrown when the whole run must end: logged out, security check, platform limit reached. */
export class StopRun extends Error {}

export function log(message: string) {
  const t = new Date().toLocaleTimeString(undefined, { hour12: false })
  console.log(`[${t}] ${message}`)
}

export function slug(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
}
