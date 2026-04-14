(function () {
    function escapeHtml(value) {
        return (value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function normalizeDelimiters(value) {
        return (value || "")
            .replace(/\\\[/g, "$$")
            .replace(/\\\]/g, "$$")
            .replace(/\\\(/g, "$")
            .replace(/\\\)/g, "$");
    }

    function stripOuterDelimiters(value) {
        var trimmed = (value || "").trim();
        if (trimmed.startsWith("$$") && trimmed.endsWith("$$") && trimmed.length >= 4) {
            return trimmed.substring(2, trimmed.length - 2);
        }

        if (trimmed.startsWith("$") && trimmed.endsWith("$") && trimmed.length >= 2) {
            return trimmed.substring(1, trimmed.length - 1);
        }

        return trimmed;
    }

    function renderMixed(container, value) {
        container.innerHTML = escapeHtml(value).replace(/\n/g, "<br />");

        if (window.renderMathInElement) {
            window.renderMathInElement(container, {
                delimiters: [
                    { left: "$$", right: "$$", display: true },
                    { left: "$", right: "$", display: false }
                ],
                throwOnError: false
            });
        }
    }

    function renderLatex(container, value, renderMode) {
        if (!window.katex) {
            renderMixed(container, value);
            return;
        }

        var expression = stripOuterDelimiters(value);
        var displayMode = renderMode === "Display";
        if (renderMode === "Auto") {
            displayMode = expression.indexOf("\n") >= 0 || expression.length > 48;
        }

        try {
            container.innerHTML = window.katex.renderToString(expression, {
                throwOnError: false,
                displayMode: displayMode,
                strict: "ignore"
            });
        } catch (error) {
            container.innerHTML = escapeHtml(value);
        }
    }

    window.mathPreview = {
        render: function (element, content, format, renderMode, emptyText) {
            if (!element) {
                return;
            }

            var normalized = normalizeDelimiters(content || "");
            if (!normalized.trim()) {
                element.textContent = emptyText || "Nema sadržaja za preview.";
                return;
            }

            if (format === "PlainText") {
                element.innerHTML = escapeHtml(normalized).replace(/\n/g, "<br />");
                return;
            }

            if (format === "Latex") {
                renderLatex(element, normalized, renderMode || "Auto");
                return;
            }

            renderMixed(element, normalized);
        }
    };
})();
