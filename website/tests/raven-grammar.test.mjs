import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

import oniguruma from 'vscode-oniguruma';
import textmate from 'vscode-textmate';

const { OnigScanner, OnigString, loadWASM } = oniguruma;
const { Registry } = textmate;

const grammarUrl = new URL('../app/components/syntaxes/raven.tmLanguage.json', import.meta.url);
const wasmUrl = new URL('../node_modules/vscode-oniguruma/release/onig.wasm', import.meta.url);

test('Raven grammar identifies representative language tokens', async () => {
  await loadWASM(await readFile(wasmUrl));
  const grammarDefinition = JSON.parse(await readFile(grammarUrl, 'utf8'));
  const registry = new Registry({
    onigLib: Promise.resolve({
      createOnigScanner: (patterns) => new OnigScanner(patterns),
      createOnigString: (text) => new OnigString(text),
    }),
    loadGrammar: async (scopeName) => (scopeName === 'source.raven' ? grammarDefinition : null),
  });
  const grammar = await registry.loadGrammar('source.raven');

  assert.ok(grammar);

  const tokens = grammar.tokenizeLine('let order = SubmitOrder("R-42") // dispatch').tokens;
  const scopes = tokens.flatMap((token) => token.scopes);

  assert.ok(scopes.some((scope) => scope.startsWith('storage.type')));
  assert.ok(scopes.some((scope) => scope.startsWith('entity.name.type')));
  assert.ok(scopes.some((scope) => scope.startsWith('string.quoted')));
  assert.ok(scopes.some((scope) => scope.startsWith('comment.line')));

  // Include escaped identifiers in the grammar-only editor fallback.
  for (const [source, expectedText, expectedScope] of [
    ['func `Calculate total price`() {}', '`Calculate total price`', 'entity.name.function.raven'],
    ['object.`Calculate total price`()', '`Calculate total price`', 'entity.name.function.raven'],
    ['let `if /* comment */` = 1', '`if /* comment */`', 'variable.other.identifier.raven'],
    ['let @if = 1', '@if', 'variable.other.identifier.raven'],
    ['object.@class()', '@class', 'entity.name.function.raven'],
    ['Optional<int>', 'Optional', 'support.type.builtin.raven'],
    ['`order-id`', '`order-id`', 'variable.other.identifier.raven'],
    ['0..=10', '..=', 'keyword.operator.raven'],
    ['checked(1 + 2)', 'checked', 'keyword.other.conversion.raven'],
  ]) {
    const token = grammar.tokenizeLine(source).tokens.find(
      ({ startIndex, endIndex }) => source.slice(startIndex, endIndex) === expectedText,
    );
    assert.ok(token?.scopes.includes(expectedScope), `Missing ${expectedScope} for ${expectedText}`);
  }
});
