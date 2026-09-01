import type { Monaco } from '@monaco-editor/react';
import { OnigScanner, OnigString, loadWASM } from 'vscode-oniguruma';
import { INITIAL, Registry, type IRawGrammar, type StateStack } from 'vscode-textmate';

import ravenGrammar from './syntaxes/raven.tmLanguage.json';

const ravenLanguageId = 'raven';
const ravenScopeName = 'source.raven';

let configuration: Promise<void> | undefined;

class RavenTokenState {
  public constructor(public readonly ruleStack: StateStack) {}

  public clone() {
    return new RavenTokenState(this.ruleStack);
  }

  public equals(other: RavenTokenState) {
    return this.ruleStack.equals(other.ruleStack);
  }
}

function tokenType(scopes: string[]) {
  const scope = scopes.join(' ');

  if (scope.includes('invalid.')) return 'invalid';
  if (scope.includes('comment')) return 'comment';
  if (scope.includes('string')) return 'string';
  if (scope.includes('constant.numeric')) return 'number';
  if (scope.includes('constant.language')) return 'constant';
  if (scope.includes('keyword') || scope.includes('storage.')) return 'keyword';
  if (scope.includes('entity.name.type') || scope.includes('support.type')) return 'type';
  if (scope.includes('entity.name.function') || scope.includes('support.function')) return 'function';
  if (scope.includes('variable.parameter')) return 'parameter';
  if (scope.includes('operator')) return 'operator';
  if (scope.includes('punctuation')) return 'delimiter';

  return '';
}

async function installRavenTokens(monaco: Monaco) {
  const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? '';
  const response = await fetch(`${basePath}/syntaxes/onig.wasm`);
  if (!response.ok) {
    throw new Error(`Unable to load the Oniguruma runtime (${response.status}).`);
  }

  await loadWASM(await response.arrayBuffer());

  const registry = new Registry({
    onigLib: Promise.resolve({
      createOnigScanner: (patterns) => new OnigScanner(patterns),
      createOnigString: (text) => new OnigString(text),
    }),
    loadGrammar: async (scopeName) =>
      scopeName === ravenScopeName ? (ravenGrammar as unknown as IRawGrammar) : null,
  });
  const grammar = await registry.loadGrammar(ravenScopeName);
  if (!grammar) {
    throw new Error('The Raven TextMate grammar could not be loaded.');
  }

  monaco.languages.setLanguageConfiguration(ravenLanguageId, {
    comments: { lineComment: '//', blockComment: ['/*', '*/'] },
    brackets: [
      ['{', '}'],
      ['[', ']'],
      ['(', ')'],
    ],
  });
  monaco.languages.setTokensProvider(ravenLanguageId, {
    getInitialState: () => new RavenTokenState(INITIAL),
    tokenize: (line, state: RavenTokenState) => {
      const result = grammar.tokenizeLine(line, state.ruleStack);
      return {
        endState: new RavenTokenState(result.ruleStack),
        tokens: result.tokens.map((token) => ({
          scopes: tokenType(token.scopes),
          startIndex: token.startIndex,
        })),
      };
    },
  });
}

export function configureRavenSyntax(monaco: Monaco) {
  configuration ??= installRavenTokens(monaco);
  return configuration;
}
