#!/usr/bin/env node
// PreToolUse guard for Bash and PowerShell commands (Claude Code hook).
// Reads the hook JSON from stdin. Exit 2 with a message on stderr blocks the command; exit 0 allows it.
// Blocks: rm -rf (or Remove-Item -Recurse -Force / rd /s) on a root, home, project or other broad path;
// git push --force/-f/--force-with-lease/+refspec; git reset --hard onto a remote ref; curl/wget/iwr piped into a shell
// or iex; `dotnet ef database drop` unless the command names a scratch database; --no-verify.
import { homedir } from 'node:os'
import { dirname, isAbsolute, parse, relative, resolve } from 'node:path'
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

/**
 * Splits a command line into simple commands, shell-style: quotes group words (and are removed), and ; & && || | and
 * newlines separate commands. Each command records whether it is fed by a pipe from the previous one.
 */
export function splitCommands(line) {
  const commands = []
  let words = []
  let word = ''
  let inWord = false
  let quote = null
  let pipedIn = false
  const endWord = () => {
    if (inWord) words.push(word)
    word = ''
    inWord = false
  }
  const endCommand = (nextPiped) => {
    endWord()
    if (words.length) commands.push({ words, pipedIn })
    words = []
    pipedIn = nextPiped
  }
  for (let i = 0; i < line.length; i++) {
    const c = line[i]
    if (quote) {
      if (c === quote) quote = null
      else if (c === '\\' && quote === '"' && i + 1 < line.length) word += line[++i]
      else word += c
      continue
    }
    if (c === '"' || c === "'") {
      quote = c
      inWord = true
      continue
    }
    if (c === '|') {
      if (line[i + 1] === '|') {
        i++
        endCommand(false)
      } else endCommand(true)
      continue
    }
    if (c === ';' || c === '\n' || c === '\r' || c === '&') {
      if (c === '&' && line[i + 1] === '&') i++
      endCommand(false)
      continue
    }
    if (c === ' ' || c === '\t') {
      endWord()
      continue
    }
    word += c
    inWord = true
  }
  endCommand(false)
  return commands
}

/** The program name, skipping env assignments and wrappers such as sudo. */
function program(words) {
  let i = 0
  while (i < words.length && (/^[A-Za-z_][A-Za-z0-9_]*=/.test(words[i]) || ['sudo', 'command', 'exec', 'nohup', 'time', 'env'].includes(words[i]))) i++
  const name = (words[i] ?? '').replace(/^.*[\\/]/, '').replace(/\.exe$/i, '').toLowerCase()
  return { name, args: words.slice(i + 1) }
}

/** Git Bash drive paths (/d/x) to Windows paths (D:\x) when running on Windows. */
function toNativePath(p) {
  if (process.platform === 'win32') {
    const m = /^\/([a-zA-Z])(\/.*)?$/.exec(p)
    if (m) return `${m[1].toUpperCase()}:${(m[2] ?? '/').replace(/\//g, '\\')}`
  }
  return p
}

/** True when deleting this path recursively would wipe a root, the home folder, the project or one of their parents. */
export function isBroadTarget(raw, cwd) {
  const t = raw.trim()
  if (!t) return false
  const lower = t.toLowerCase()
  if (['*', '.*', './*', '.\\*'].includes(lower)) return true
  if (/^(~|\$home|\$\{home\}|\$env:userprofile|\$env:homepath|%userprofile%|%homepath%)([\\/]\*?)?$/.test(lower)) return true
  if (/^(\/|\\)\*?$/.test(t)) return true // / and /*
  if (/^[a-z]:[\\/]?\*?$/i.test(t)) return true // C:, C:\, C:/*
  if (/^\/[a-z]\/?\*?$/i.test(t)) return true // Git Bash drive roots /c, /d/
  if (/^\/mnt\/[a-z]\/?\*?$/i.test(t)) return true // WSL drive roots
  if (lower.startsWith('~') || lower.startsWith('$home') || lower.startsWith('$env:userprofile')) return false

  const withoutGlob = t.replace(/[\\/]\*$/, '') || t
  let target
  try {
    const native = toNativePath(withoutGlob)
    target = resolve(isAbsolute(native) ? native : resolve(cwd, native))
  } catch {
    return false
  }
  const covers = (protectedPath) => {
    const rel = relative(target, resolve(protectedPath))
    return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel)) // target is the path itself or one of its parents
  }
  if (target === parse(target).root) return true
  if (covers(homedir()) || covers(PROJECT_DIR)) return true
  // System folders.
  return /^([a-z]:)?[\\/](windows|program files( \(x86\))?|programdata|users|etc|usr|var|bin|boot|lib|opt|home|root)[\\/]?$/i.test(
    target.replace(/^[a-z]:/i, (d) => d.toLowerCase()),
  )
}

function checkRecursiveDelete({ name, args }, cwd) {
  let recursive = false
  let force = false
  const targets = []
  if (name === 'rm') {
    for (const a of args) {
      if (a === '--') continue
      if (/^--recursive$/i.test(a)) recursive = true
      else if (/^--force$/i.test(a)) force = true
      else if (/^--no-preserve-root$/i.test(a)) return 'rm --no-preserve-root is never allowed.'
      else if (/^-recurse$/i.test(a)) recursive = true // PowerShell alias rm -> Remove-Item
      else if (/^-force$/i.test(a)) force = true
      else if (/^-[a-zA-Z]+$/.test(a)) {
        if (/[rR]/.test(a)) recursive = true
        if (/f/i.test(a)) force = true
      } else if (!a.startsWith('-')) targets.push(a)
    }
  } else if (['remove-item', 'ri', 'del', 'erase', 'rmdir', 'rd'].includes(name)) {
    for (let i = 0; i < args.length; i++) {
      const a = args[i]
      if (/^-r(e(c(u(r(s(e)?)?)?)?)?)?$/i.test(a) || /^\/s$/i.test(a)) recursive = true
      else if (/^-fo(r(c(e)?)?)?$/i.test(a) || /^\/q$/i.test(a)) force = true
      else if (/^-(path|literalpath)$/i.test(a) && args[i + 1]) targets.push(args[++i])
      else if (!a.startsWith('-') && !/^\/[a-z]$/i.test(a)) targets.push(a)
    }
    if (name === 'rd' || name === 'rmdir') force = force || recursive
  } else return null

  if (!(recursive && force)) return null
  const broad = targets.find((t) => t.split(',').some((part) => isBroadTarget(part, cwd)))
  return broad === undefined
    ? null
    : `recursive force delete of "${broad}" would remove a root, home, project or system folder. Delete specific files or folders inside the project instead.`
}

function checkGit({ name, args }) {
  if (name !== 'git') return null
  let sub = 0
  while (sub < args.length && args[sub].startsWith('-')) sub += /^(-C|-c|--git-dir|--work-tree|--namespace)$/.test(args[sub]) ? 2 : 1
  const verb = args[sub]
  const rest = args.slice(sub + 1)
  if (verb === 'push') {
    if (rest.some((a) => /^--force(-with-lease|-if-includes)?(=.*)?$/.test(a) || /^-[a-zA-Z]*f[a-zA-Z]*$/.test(a)))
      return 'force-pushing rewrites shared history. Push normally; if history must be rewritten, the user does it.'
    if (rest.some((a) => /^\+[^+]/.test(a))) return 'a "+refspec" push is a force push. Push normally.'
  }
  if (verb === 'reset' && rest.includes('--hard') && rest.some((a) => /(^(origin|upstream)(\/|$))|(@\{(u|upstream|push)\})/i.test(a)))
    return 'git reset --hard onto a remote ref discards local work. Use git stash or a new branch, or ask the user.'
  return null
}

function checkPipeToShell(commands, raw) {
  const fetchers = new Set(['curl', 'wget', 'iwr', 'irm', 'invoke-webrequest', 'invoke-restmethod'])
  const shells = new Set(['sh', 'bash', 'zsh', 'dash', 'ksh', 'fish', 'pwsh', 'powershell', 'iex', 'invoke-expression', 'python', 'python3', 'node', 'perl', 'ruby', 'cmd'])
  for (let i = 1; i < commands.length; i++) {
    if (!commands[i].pipedIn) continue
    let j = i - 1
    while (j > 0 && commands[j].pipedIn) j-- // the first command of this pipeline
    const fed = commands.slice(j, i).some((c) => fetchers.has(program(c.words).name))
    if (fed && shells.has(program(commands[i].words).name)) return 'downloaded content piped into a shell runs unreviewed code. Download the file, read it, then run it.'
  }
  if (/\b(?:ba|z|da|k)?sh\b[^\n;|&]*(?:\$\(|`|<\()\s*(?:curl|wget)\b/i.test(raw))
    return 'a shell running downloaded content runs unreviewed code. Download the file, read it, then run it.'
  if (/\b(?:iex|invoke-expression)\b[\s(]*(?:\$\()?\s*\(?\s*(?:iwr|irm|curl|wget|invoke-webrequest|invoke-restmethod|new-object\s+(?:system\.)?net\.webclient)/i.test(raw))
    return 'Invoke-Expression on downloaded content runs unreviewed code. Download the file, read it, then run it.'
  return null
}

function checkEfDrop({ name, args }, raw) {
  const isEf = (name === 'dotnet' && args[0]?.toLowerCase() === 'ef') || name === 'dotnet-ef'
  if (!isEf) return null
  const a = name === 'dotnet' ? args.slice(1) : args
  if (a[0]?.toLowerCase() === 'database' && a[1]?.toLowerCase() === 'drop' && !/scratch/i.test(raw))
    return '`dotnet ef database drop` is only allowed against a scratch database: put "scratch" in the database name or connection string.'
  return null
}

/** The script handed to a nested shell: bash -c "…", pwsh -Command "…", cmd /c …. */
function nestedScript({ name, args }) {
  const flag = {
    sh: /^-c$/, bash: /^-c$/, zsh: /^-c$/, dash: /^-c$/,
    pwsh: /^-(c|command)$/i, powershell: /^-(c|command)$/i,
    cmd: /^\/{1,2}[ck]$/i,
  }[name]
  if (!flag) return null
  const i = args.findIndex((a) => flag.test(a))
  return i >= 0 && i + 1 < args.length ? args.slice(i + 1).join(' ') : null
}

export function check(command, cwd = process.cwd(), depth = 0) {
  const raw = String(command ?? '')
  if (!raw.trim()) return null
  const commands = splitCommands(raw)
  for (const c of commands) {
    if (c.words.includes('--no-verify')) return '--no-verify skips the repository hooks. Fix what the hook reports instead.'
    const p = program(c.words)
    const nested = depth < 3 ? nestedScript(p) : null
    const reason = checkRecursiveDelete(p, cwd) ?? checkGit(p) ?? checkEfDrop(p, raw) ?? (nested ? check(nested, cwd, depth + 1) : null)
    if (reason) return reason
  }
  return checkPipeToShell(commands, raw)
}

async function main() {
  let input
  try {
    input = JSON.parse(await readStdin())
  } catch {
    process.exit(0) // not a hook payload; nothing to judge
  }
  const command = input?.tool_input?.command
  if (typeof command !== 'string') process.exit(0)
  const reason = check(command, input.cwd || process.cwd())
  if (reason) {
    process.stderr.write(`Blocked by .claude/hooks/guard-command.mjs: ${reason}\n`)
    process.exit(2)
  }
  process.exit(0)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) void main()
