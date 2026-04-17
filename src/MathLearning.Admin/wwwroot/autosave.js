export function saveDraft(key, json) {
    try {
        localStorage.setItem(key, json);
        return true;
    } catch (e) {
        return false;
    }
}

export function loadDraft(key) {
    try {
        return localStorage.getItem(key);
    } catch (e) {
        return null;
    }
}

export function clearDraft(key) {
    try {
        localStorage.removeItem(key);
    } catch (e) { }
}
