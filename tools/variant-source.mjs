import assert from 'node:assert/strict';
// Preserve strings while comparing code; comments and formatting are not behavior.
const lexical = /\/\/[^\n]*|\/\*[\s\S]*?\*\/|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\[\s\S]|[^"\\])*"|'(?:\\[\s\S]|[^'\\])*'|\b[A-Za-z_]\w*\b/g;
export function canonicalCode(text) {
  let result = '', last = 0;
  for (const match of text.matchAll(lexical)) {
    result += text.slice(last, match.index).replace(/\s/g, '');
    if (!match[0].startsWith('//') && !match[0].startsWith('/*')) result += match[0];
    last = match.index + match[0].length;
  }
  return result + text.slice(last).replace(/\s/g, '');
}
export function selectVariant(text, variant) {
  // GAME_BETA is the historic name for the shared 0.111+ API surface.
  const defined = new Set(['WEI_DONG_ZHUO', 'GAME_BETA']);
  if (variant === 'beta') {
    defined.add('GAME_V0113');
    defined.add('GAME_V0114');
  }
  const stack = []; const output = []; let active = true;
  for (const line of text.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').split('\n')) {
    const directive = line.match(/^\s*#(if|else|endif)\b\s*(.*)$/);
    if (!directive) { if (active) output.push(line); continue; }
    const [, command, expression] = directive;
    if (command === 'if') {
      const symbol = expression.trim();
      assert(/^!?\w+$/.test(symbol), `Unsupported directive: ${line}`);
      const value = symbol.startsWith('!') ? !defined.has(symbol.slice(1)) : defined.has(symbol);
      stack.push({ parent: active, condition: value }); active = active && value;
    } else if (command === 'else') {
      assert(stack.length, 'Unbalanced #else');
      const state = stack.at(-1); active = state.parent && !state.condition;
    } else {
      assert(stack.length, 'Unbalanced #endif'); active = stack.pop().parent;
    }
  }
  assert.equal(stack.length, 0, 'Unbalanced conditional source');
  return output.join('\n');
}
export function simplifyBranches(text) {
  const pattern = /^#if GAME_BETA\n((?:(?!^#(?:if|else|endif)\b)[\s\S])*?)^#else\n((?:(?!^#(?:if|else|endif)\b)[\s\S])*?)^#endif\n/gm;
  let previous;
  do {
    previous = text;
    text = text.replace(pattern, (whole, beta, stable) => canonicalCode(beta) === canonicalCode(stable) ? stable : whole);
  } while (text !== previous);
  return text;
}
export function conditionalLines(stable, toBeta) {
  return stable.split('\n').map(line => {
    const beta = toBeta(line);
    return beta === line ? line : `#if GAME_BETA\n${beta}\n#else\n${line}\n#endif`;
  }).join('\n');
}
