window.mentionTextarea = window.mentionTextarea || {};

/**
 * Attaches a keydown interceptor to the textarea that handles dropdown navigation.
 * ArrowUp/Down, Enter and Escape are intercepted when the dropdown is open.
 */
window.mentionTextarea.init = function (element, dotNetRef) {
    if (!element) return;

    element._mentionDropdownOpen = false;
    element._mentionDotNet = dotNetRef;

    element._mentionKeyHandler = function (e) {
        if (!element._mentionDropdownOpen) return;

        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('KeyboardNavigateAsync', e.key);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('SelectCurrentAsync');
        } else if (e.key === 'Tab') {
            // Allow normal Tab navigation; close the dropdown without selecting
            element._mentionDropdownOpen = false;
            dotNetRef.invokeMethodAsync('EscapeAsync');
        } else if (e.key === 'Escape') {
            element._mentionDropdownOpen = false;
            dotNetRef.invokeMethodAsync('EscapeAsync');
        }
    };

    element.addEventListener('keydown', element._mentionKeyHandler);
};

/**
 * Returns the current caret (cursor) position inside the textarea.
 */
window.mentionTextarea.getCaretPosition = function (element) {
    return element ? element.selectionStart : 0;
};

/**
 * Sets the textarea value and moves the caret to the given position.
 * Does NOT dispatch an input event because Blazor already tracks the value
 * via ValueChanged before this call is made.
 */
window.mentionTextarea.setValueAndCaret = function (element, text, caretPos) {
    if (!element) return;
    element.value = text;
    element.selectionStart = caretPos;
    element.selectionEnd = caretPos;
    element.focus();
};

/**
 * Communicates to the JS side whether the suggestion dropdown is currently visible.
 * This controls whether keydown events are intercepted.
 */
window.mentionTextarea.setDropdownOpen = function (element, isOpen) {
    if (element) {
        element._mentionDropdownOpen = isOpen;
    }
};

/**
 * Removes the keydown event listener and cleans up stored references.
 */
window.mentionTextarea.dispose = function (element) {
    if (!element) return;
    if (element._mentionKeyHandler) {
        element.removeEventListener('keydown', element._mentionKeyHandler);
        element._mentionKeyHandler = null;
    }
    element._mentionDotNet = null;
};
