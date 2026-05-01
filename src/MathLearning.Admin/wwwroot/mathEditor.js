/**
 * mathEditor.js — LaTeX authoring helpers for the MathLearning Admin editor.
 *
 * Exports:
 *   insertLatexAtCursor(template)  — inserts LaTeX template at the active element's cursor.
 *   registerCtrlM(dotNetRef)       — registers a global Ctrl+M handler that wraps selection in $…$.
 *   unregisterCtrlM()              — removes the global Ctrl+M handler.
 */

let _ctrlMHandler = null;
let _focusInHandler = null;
let _selectionHandler = null;
let _lastFocusedField = null;
let _lastSelection = null;

const textFieldSelector = [
    'textarea[data-latex-field]',
    'input[data-latex-field]',
    '[data-latex-field] textarea',
    '[data-latex-field] input'
].join(', ');

function isTextField(el) {
    return !!el && (el.tagName === 'TEXTAREA' || el.tagName === 'INPUT');
}

function getFieldHost(el) {
    if (!el) return null;
    if (el.hasAttribute?.('data-latex-field')) return el;
    return el.closest?.('[data-latex-field]') ?? null;
}

function getFieldId(el) {
    return getFieldHost(el)?.getAttribute('data-latex-field') ?? null;
}

function isLatexTextField(el) {
    return isTextField(el) && !!getFieldId(el);
}

function rememberField(el) {
    if (!isLatexTextField(el)) return;

    _lastFocusedField = el;
    _lastSelection = {
        el,
        start: el.selectionStart ?? el.value.length,
        end: el.selectionEnd ?? el.value.length
    };
}

function getFallbackField() {
    return document.querySelector('textarea[data-latex-default="true"], input[data-latex-default="true"], [data-latex-default="true"] textarea, [data-latex-default="true"] input')
        || document.querySelector(textFieldSelector);
}

function getTargetField() {
    const active = document.activeElement;
    if (isLatexTextField(active)) {
        rememberField(active);
        return active;
    }

    if (isLatexTextField(_lastFocusedField) && document.contains(_lastFocusedField)) {
        return _lastFocusedField;
    }

    return getFallbackField();
}

function setNativeValue(el, value) {
    const proto = el.tagName === 'TEXTAREA'
        ? window.HTMLTextAreaElement.prototype
        : window.HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
    if (setter) {
        setter.call(el, value);
    } else {
        el.value = value;
    }
}

function notifyValueChanged(el) {
    el.dispatchEvent(new InputEvent('input', {
        bubbles: true,
        inputType: 'insertText',
        data: null
    }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
}

function failure() {
    return {
        succeeded: false,
        fieldId: null,
        value: null,
        selectionStart: 0,
        selectionEnd: 0,
        usedFallback: false
    };
}

/**
 * Inserts a LaTeX template at the cursor position of the currently focused textarea/input,
 * or the last known focused field if focus has moved to a toolbar button.
 * The first `{}` in the template is replaced by any currently selected text.
 * After insertion the cursor is placed inside the first `{}` position (or after the text).
 *
 * @param {string} template  e.g. "\\frac{}{}", "${}$", "\\sqrt{}"
 * @returns {boolean} true if insertion succeeded
 */
export function insertLatexAtCursor(template) {
    const el = getTargetField();
    if (!el) return failure();

    const hadRememberedFocus = isLatexTextField(_lastFocusedField) && document.contains(_lastFocusedField);
    const useRememberedSelection = _lastSelection?.el === el;
    const start = useRememberedSelection ? _lastSelection.start : (el.selectionStart ?? el.value.length);
    const end = useRememberedSelection ? _lastSelection.end : (el.selectionEnd ?? el.value.length);
    const selected = el.value.substring(start, end);

    // Replace first {} placeholder with any selected text
    let toInsert = template;
    if (selected.length > 0 && template.includes('{}')) {
        toInsert = template.replace('{}', selected);
    }

    const newValue = el.value.substring(0, start) + toInsert + el.value.substring(end);

    setNativeValue(el, newValue);

    // Fire events so Blazor picks up the new value
    notifyValueChanged(el);

    const cursorPos = start + toInsert.length;
    el.focus({ preventScroll: true });
    el.setSelectionRange(cursorPos, cursorPos);
    rememberField(el);

    return {
        succeeded: true,
        fieldId: getFieldId(el),
        value: el.value,
        selectionStart: cursorPos,
        selectionEnd: cursorPos,
        usedFallback: !hadRememberedFocus
    };
}

/**
 * Registers a document-level keydown handler for Ctrl+M that wraps the current
 * textarea selection in $…$ (or $$…$$ if the selection contains a newline).
 *
 * @param {object} dotNetRef  DotNet object reference (not used currently, kept for future callbacks)
 */
export function registerCtrlM(dotNetRef) {
    if (_ctrlMHandler) return; // already registered

    // Track the last focused text field so toolbar buttons can insert even after stealing focus
    if (!_focusInHandler) {
        _focusInHandler = (e) => {
            rememberField(e.target);
        };
        document.addEventListener('focusin', _focusInHandler);
    }

    if (!_selectionHandler) {
        _selectionHandler = (e) => {
            rememberField(e.target);
        };
        document.addEventListener('keyup', _selectionHandler, true);
        document.addEventListener('mouseup', _selectionHandler, true);
        document.addEventListener('select', _selectionHandler, true);
        document.addEventListener('input', _selectionHandler, true);
    }

    _ctrlMHandler = (e) => {
        if (!e.ctrlKey || e.key !== 'm') return;
        const el = document.activeElement;
        if (!isLatexTextField(el)) return;

        e.preventDefault();

        const start = el.selectionStart ?? 0;
        const end = el.selectionEnd ?? 0;
        const selected = el.value.substring(start, end);
        const useDisplay = selected.includes('\n');
        const before = el.value.substring(0, start);
        const after = el.value.substring(end);
        const newValue = before + (useDisplay ? `$$${selected}$$` : `$${selected}$`) + after;

        setNativeValue(el, newValue);
        notifyValueChanged(el);

        const newCursor = start + (useDisplay ? 2 : 1);
        const newEnd = newCursor + selected.length;
        el.focus({ preventScroll: true });
        el.setSelectionRange(newCursor, newEnd);
        rememberField(el);
    };

    document.addEventListener('keydown', _ctrlMHandler);
}

/**
 * Removes the global Ctrl+M handler registered by registerCtrlM().
 */
export function unregisterCtrlM() {
    if (_ctrlMHandler) {
        document.removeEventListener('keydown', _ctrlMHandler);
        _ctrlMHandler = null;
    }
    if (_focusInHandler) {
        document.removeEventListener('focusin', _focusInHandler);
        _focusInHandler = null;
    }
    if (_selectionHandler) {
        document.removeEventListener('keyup', _selectionHandler, true);
        document.removeEventListener('mouseup', _selectionHandler, true);
        document.removeEventListener('select', _selectionHandler, true);
        document.removeEventListener('input', _selectionHandler, true);
        _selectionHandler = null;
    }
    _lastFocusedField = null;
    _lastSelection = null;
}
