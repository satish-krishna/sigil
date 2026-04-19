import { readFileSync } from 'fs';
import { join } from 'path';

const pluginRoot = join(import.meta.dir, '..');

function readFile(path: string): string {
  try {
    return readFileSync(path, 'utf-8');
  } catch {
    return '';
  }
}

const persona = readFile(join(pluginRoot, 'persona', 'BOB.md'));

const parts: string[] = [];

if (persona) {
  parts.push(
    '<EXTREMELY_IMPORTANT>\n# Output Style: Bob the Skull\n\n' +
      persona +
      '\n\n</EXTREMELY_IMPORTANT>'
  );
}


console.error('✓ session-start hook ran successfully');
console.log(JSON.stringify({ additionalContext: parts.join('\n\n') }));
