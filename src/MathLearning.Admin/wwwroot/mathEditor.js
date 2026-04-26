/**
 * mathEditor.js — LaTeX authoring helpers for the MathLearning Admin editor.
 *
 * Exports:
 *   insertLatexAtCursor(template)  — inserts LaTeX template at the active element's cursor.
 *   registerCtrlM(dotNetRef)       — registers a global Ctrl+M handler that wraps selection in $…$.
 *   unregisterCtrlM()              — removes the global Ctrl+M handler.
 */

let _ctrlMHandler = null;

/**
 * Inserts a LaTeX template at the cursor position of the currently focused textarea/input.
 * The first `{}` in the template is replaced by any currently selected text.
 * After insertion the cursor is placed inside the first `{}` position (or after the text).
 *
 * @param {string} template  e.g. "\\frac{}{}", "${}$", "\\sqrt{}"
 * @returns {boolean} true if insertion succeeded
 */
export function insertLatexAtCursor(template) {
    const el = document.activeElement;
    if (!el || (el.tagName !== 'TEXTAREA' && el.tagName !== 'INPUT')) {
        return false;
    }

    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? el.value.length;
    const selected = el.value.substring(start, end);

    // Replace first {} placeholder with any selected text
    let toInsert = template;
    if (selected.length > 0 && template.includes('{}')) {
        toInsert = template.replace('{}', selected);
    }

    const newValue = el.value.substring(0, start) + toInsert + el.value.substring(end);

    // Use native setter so JS framework event listeners fire correctly
    const proto = el.tagName === 'TEXTAREA'
        ? window.HTMLTextAreaElement.prototype
        : window.HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
    if (setter) {
        setter.call(el, newValue);
    } else {
        el.value = newValue;
    }

    // Fire events so Blazor picks up the new value
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));

    // Place cursor at the first {} placeholder position inside the inserted text,
    // so the author can immediately type the argument.
    const placeholderIndex = toInsert.indexOf('{}');
    const cursorPos = start + (placeholderIndex >= 0 ? placeholderIndex : toInsert.length);
    el.setSelectionRange(cursorPos, cursorPos);
    el.focus();
    return true;
}

/**
 * Registers a document-level keydown handler for Ctrl+M that wraps the current
 * textarea selection in $…$ (or $$…$$ if the selection contains a newline).
 *
 * @param {object} dotNetRef  DotNet object reference (not used currently, kept for future callbacks)
 */
export function registerCtrlM(dotNetRef) {
    if (_ctrlMHandler) return; // already registered

    _ctrlMHandler = (e) => {
        if (!e.ctrlKey || e.key !== 'm') return;
        const el = document.activeElement;
        if (!el || (el.tagName !== 'TEXTAREA' && el.tagName !== 'INPUT')) return;

        e.preventDefault();

        const start = el.selectionStart ?? 0;
        const end = el.selectionEnd ?? 0;
        const selected = el.value.substring(start, end);
        const useDisplay = selected.includes('\n');
        const wrapped = useDisplay ? `$$${selected}$$` : `$${selected}$`;
        insertLatexAtCursor(wrapped.replace(selected, selected || ''));

        // Re-apply since insertLatexAtCursor replaces {} not the selected text directly
        // Simpler: do the wrapping inline here
        const before = el.value.substring(0, start);
        const after = el.value.substring(end);
        const newValue = before + (useDisplay ? `$$${selected}$$` : `$${selected}$`) + after;

        const proto = el.tagName === 'TEXTAREA'
            ? window.HTMLTextAreaElement.prototype
            : window.HTMLInputElement.prototype;
        const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
        if (setter) { setter.call(el, newValue); } else { el.value = newValue; }
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));

        const newCursor = start + (useDisplay ? 2 : 1);
        const newEnd = newCursor + selected.length;
        el.setSelectionRange(newCursor, newEnd);
        el.focus();
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
}
