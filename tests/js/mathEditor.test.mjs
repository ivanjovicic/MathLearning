import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { Buffer } from 'node:buffer';

class FakeTextArea {
  constructor(attributes = {}) {
    this.tagName = 'TEXTAREA';
    this.value = '';
    this.selectionStart = 0;
    this.selectionEnd = 0;
    this.attributes = new Map(Object.entries(attributes));
    this.dispatchedEvents = [];
  }

  hasAttribute(name) {
    return this.attributes.has(name);
  }

  getAttribute(name) {
    return this.attributes.get(name) ?? null;
  }

  focus() {
    globalThis.document.activeElement = this;
    globalThis.document.dispatchToListeners('focusin', { target: this });
  }

  setSelectionRange(start, end) {
    this.selectionStart = start;
    this.selectionEnd = end;
    globalThis.document.dispatchToListeners('select', { target: this });
  }

  dispatchEvent(event) {
    this.dispatchedEvents.push(event.type);
    globalThis.document.dispatchToListeners(event.type, { target: this });
    return true;
  }
}

class FakeInput extends FakeTextArea {
  constructor(attributes = {}) {
    super(attributes);
    this.tagName = 'INPUT';
  }
}

class FakeButton {
  constructor() {
    this.tagName = 'BUTTON';
  }

  hasAttribute() {
    return false;
  }

  focus() {
    globalThis.document.activeElement = this;
    globalThis.document.dispatchToListeners('focusin', { target: this });
  }
}

async function loadModule() {
  const listeners = new Map();
  const elements = [];

  globalThis.InputEvent = class {
    constructor(type, init = {}) {
      this.type = type;
      Object.assign(this, init);
    }
  };
  globalThis.Event = class {
    constructor(type, init = {}) {
      this.type = type;
      Object.assign(this, init);
    }
  };
  globalThis.window = {
    HTMLTextAreaElement: FakeTextArea,
    HTMLInputElement: FakeInput
  };
  globalThis.document = {
    activeElement: null,
    elements,
    addEventListener(type, handler) {
      const handlers = listeners.get(type) ?? [];
      handlers.push(handler);
      listeners.set(type, handlers);
    },
    removeEventListener(type, handler) {
      listeners.set(type, (listeners.get(type) ?? []).filter(x => x !== handler));
    },
    dispatchToListeners(type, event) {
      for (const handler of listeners.get(type) ?? []) {
        handler(event);
      }
    },
    contains(el) {
      return elements.includes(el);
    },
    querySelector(selector) {
      if (selector.includes('data-latex-default="true"')) {
        return elements.find(x => x.getAttribute?.('data-latex-default') === 'true') ?? null;
      }

      return elements.find(x => x.hasAttribute?.('data-latex-field')) ?? null;
    }
  };

  const source = readFileSync(new URL('../../src/MathLearning.Admin/wwwroot/mathEditor.js', import.meta.url), 'utf8');
  const encoded = Buffer.from(`${source}\n// ${Date.now()}-${Math.random()}`).toString('base64');
  const mod = await import(`data:text/javascript;base64,${encoded}`);

  return { mod, elements };
}

test('focused question textarea receives quick insert and caret moves after snippet', async () => {
  const { mod, elements } = await loadModule();
  const question = new FakeTextArea({
    'data-latex-field': 'question.text',
    'data-latex-default': 'true'
  });
  question.value = 'ab';
  elements.push(question);

  mod.registerCtrlM({});
  question.focus();
  question.setSelectionRange(1, 1);

  const result = mod.insertLatexAtCursor('\\pi');

  assert.equal(result.succeeded, true);
  assert.equal(result.fieldId, 'question.text');
  assert.equal(question.value, 'a\\pib');
  assert.equal(question.selectionStart, 4);
  assert.equal(question.selectionEnd, 4);
  assert.deepEqual(question.dispatchedEvents, ['input', 'change']);
});

test('selection is replaced by snippet when toolbar button steals focus', async () => {
  const { mod, elements } = await loadModule();
  const question = new FakeTextArea({
    'data-latex-field': 'question.text',
    'data-latex-default': 'true'
  });
  question.value = 'replace me';
  const button = new FakeButton();
  elements.push(question);

  mod.registerCtrlM({});
  question.focus();
  question.setSelectionRange(0, 7);
  button.focus();

  const result = mod.insertLatexAtCursor('\\pi');

  assert.equal(result.succeeded, true);
  assert.equal(question.value, '\\pi me');
  assert.equal(question.selectionStart, 3);
  assert.equal(globalThis.document.activeElement, question);
});

test('selected text fills first placeholder when template supports wrapping', async () => {
  const { mod, elements } = await loadModule();
  const question = new FakeTextArea({
    'data-latex-field': 'question.text',
    'data-latex-default': 'true'
  });
  question.value = 'x';
  elements.push(question);

  mod.registerCtrlM({});
  question.focus();
  question.setSelectionRange(0, 1);

  mod.insertLatexAtCursor('$...$'.replace('...', '{}'));

  assert.equal(question.value, '$x$');
});

test('default question textarea is used when there is no valid focused field', async () => {
  const { mod, elements } = await loadModule();
  const question = new FakeTextArea({
    'data-latex-field': 'question.text',
    'data-latex-default': 'true'
  });
  question.value = 'tekst';
  question.setSelectionRange(question.value.length, question.value.length);
  elements.push(question);

  const result = mod.insertLatexAtCursor('\\frac{}{}');

  assert.equal(result.succeeded, true);
  assert.equal(result.usedFallback, true);
  assert.equal(question.value, 'tekst\\frac{}{}');
});
