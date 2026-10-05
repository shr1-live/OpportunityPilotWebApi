#!/usr/bin/env node
// PostToolUse hook for Edit, Write and MultiEdit (Claude Code). Never blocks: always exits 0.
// Appends one line per write to .claude/hooks/write.log (git-ignored): time, tool, kind, path. Kept fast on purpose —
// formatting is not run here (`dotnet format` takes seconds per call); the 0-warning build is the gate.
import { appendFileSync } from 'node:fs'
import { dirname, extname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HOOK_DIR = dirname(fileURLToPath(import.meta.url))
const PROJECT_DIR = resolve(process.env.CLAUDE_PROJECT_DIR || resolve(HOOK_DIR, '..', '..'))

const KINDS = { '.cs': 'csharp', '.csproj': 'msbuild', '.ts': 'typescript', '.mjs': 'javascript', '.md': 'docs', '.json': 'json', '.yaml': 'yaml', '.yml': 'yaml' }

let data = ''
process.stdin.setEncoding('utf8')
process.stdin.on('data', (chunk) => (data += chunk))
process.stdin.on('end', () => {
  try {
    const payload = JSON.parse(data)
    const input = payload?.tool_input ?? {}
    const filePath = input.file_path ?? input.notebook_path
    if (typeof filePath === 'string' && filePath) {
      const absolute = resolve(isAbsolute(filePath) ? filePath : resolve(payload.cwd || process.cwd(), filePath))
      const rel = relative(PROJECT_DIR, absolute)
      const shown = (rel && !rel.startsWith('..') && !isAbsolute(rel) ? rel : absolute).replace(/\\/g, '/')
      const kind = KINDS[extname(absolute).toLowerCase()] ?? 'other'
      appendFileSync(join(HOOK_DIR, 'write.log'), `${new Date().toISOString()}\t${payload.tool_name ?? '?'}\t${kind}\t${shown}\n`)
    }
  } catch {
    // Logging must never get in the way of the edit.
  }
  process.exit(0)
})
process.stdin.on('error', () => process.exit(0))
