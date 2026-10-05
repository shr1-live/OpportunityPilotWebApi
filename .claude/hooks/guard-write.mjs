#!/usr/bin/env node
// PreToolUse guard for Edit, Write, MultiEdit and NotebookEdit (Claude Code hook).
// Reads the hook JSON from stdin. Exit 2 with a message on stderr blocks the write; exit 0 allows it.
// Blocks writes to secret/local files (.env*, appsettings.*.local.json, lock files, agent/config.json, agent/.data/**)
// and content that looks like a real secret. Fake opk_ agent keys and test passwords are allowed under tests/ and agent/test/.
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const PROJECT_DIR = resolve(process.env.CLAUDE_PROJECT_DIR || resolve(dirname(fileURLToPath(import.meta.url)), '..', '..'))

function readStdin() {
  return new Promise((done) => {
    let data = ''
    process.stdin.setEncoding('utf8')
    process.stdin.on('data', (chunk) => (data += chunk))
    process.stdin.on('end', () => done(data))
    process.stdin.on('error', () => done(data))
  })
}

/** Path relative to the project with forward slashes, or the absolute path when outside it. */
function projectPath(filePath, cwd) {
  const absolute = resolve(isAbsolute(filePath) ? filePath : resolve(cwd, filePath))
  const rel = relative(PROJECT_DIR, absolute)
  return (rel && !rel.startsWith('..') && !isAbsolute(rel) ? rel : absolute).replace(/\\/g, '/')
}

export function checkPath(path) {
  const name = basename(path).toLowerCase()
  if (/^\.env($|\.)/.test(name) && name !== '.env.example')
    return `${path} holds local secrets. Edit .env.example (placeholders only) or ask the user to change it.`
  if (/^appsettings\..+\.local\.json$/.test(name)) return `${path} is a local, secret-bearing settings file. Use user-secrets or ask the user.`
  if (name === 'package-lock.json' || name === 'pnpm-lock.yaml' || name.endsWith('.lock'))
    return `${path} is a lock file. Change package.json and run the package manager (npm install) instead of editing it.`
  if (/(^|\/)agent\/config\.json$/i.test(path)) return `${path} holds the user's agent key and answers. Only the user edits it.`
  if (/(^|\/)agent\/\.data(\/|$)/i.test(path)) return `${path} is the agent's browser session and local log. Never edit it.`
  return null
}

// Values that are clearly placeholders rather than real passwords.
const PLACEHOLDER = /^(\.\.\.|…|<[^>]*>|\{[^}]*\}|\$\{?[A-Za-z_]\w*\}?|%[A-Za-z_]+%|\*+|x+|changeme|change-me|password|your[-_]?password|postgres|secret|example|placeholder|test|dev)$/i

// A line is a connection string when it also names a server, database or user (so `var password = x;` is not one).
const CONNECTION_KEY = /(?:^|[;"'\s])(?:host|server|data source|database|initial catalog|user ?id|username|uid)\s*=/i

function realPasswords(content) {
  const found = []
  for (const line of content.split(/\r?\n/)) {
    if (!CONNECTION_KEY.test(line)) continue
    for (const m of line.matchAll(/(?:^|[;"'\s])(?:password|pwd)\s*=\s*([^;"'\s]*)/gi)) {
      if (m[1] && !PLACEHOLDER.test(m[1])) found.push(m[1])
    }
  }
  for (const m of content.matchAll(/postgres(?:ql)?:\/\/[^:\s/@]+:([^@\s/]+)@/gi)) {
    if (!PLACEHOLDER.test(m[1])) found.push(m[1])
  }
  return found
}

export function checkContent(path, content) {
  if (!content) return null
  const isTestFixture = /^(tests|agent\/test)\//.test(path)
  if (!isTestFixture && /opk_[A-Za-z0-9_-]{30,}/.test(content))
    return 'the content contains what looks like a real OpportunityPilot agent key (opk_…). Never write keys into files; fake keys belong only under tests/ or agent/test/.'
  if (/AIza[0-9A-Za-z_-]{30,}/.test(content)) return 'the content contains what looks like a Google/Gemini API key (AIza…). Keys go in server configuration, never in files.'
  if (/(?<![A-Za-z0-9_-])sk-(?:[A-Za-z0-9]+-)*[A-Za-z0-9_]{20,}/.test(content))
    return 'the content contains what looks like a secret API key (sk-…). Keys go in configuration or user-secrets, never in files.'
  if (/-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----/.test(content)) return 'the content contains a private key. Never write private keys into the repository.'
  if (!isTestFixture && realPasswords(content).length)
    return 'the content contains a connection string with a real-looking password. Use a placeholder (Password=...) and keep the value in user-secrets or environment variables.'
  return null
}

/** The text a tool call would write. */
function contentOf(toolName, input) {
  switch (toolName) {
    case 'Write':
      return input.content ?? ''
    case 'Edit':
      return input.new_string ?? ''
    case 'MultiEdit':
      return (input.edits ?? []).map((e) => e?.new_string ?? '').join('\n')
    case 'NotebookEdit':
      return input.new_source ?? ''
    default:
      return input.content ?? input.new_string ?? ''
  }
}

async function main() {
  let payload
  try {
    payload = JSON.parse(await readStdin())
  } catch {
    process.exit(0) // not a hook payload; nothing to judge
  }
  const input = payload?.tool_input ?? {}
  const filePath = input.file_path ?? input.notebook_path
  if (typeof filePath !== 'string' || !filePath) process.exit(0)
  const path = projectPath(filePath, payload.cwd || process.cwd())
  const reason = checkPath(path) ?? checkContent(path, String(contentOf(payload.tool_name, input)))
  if (reason) {
    process.stderr.write(`Blocked by .claude/hooks/guard-write.mjs: ${reason}\n`)
    process.exit(2)
  }
  process.exit(0)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) void main()
