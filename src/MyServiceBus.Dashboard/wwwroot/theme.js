(() => {
    const storageKey = "myservicebus.dashboard.theme";
    const media = window.matchMedia("(prefers-color-scheme: dark)");

    function getPreference() {
        const value = localStorage.getItem(storageKey);
        return value === "dark" || value === "light" ? value : "system";
    }

    function apply(preference) {
        const resolved = preference === "system" ? (media.matches ? "dark" : "light") : preference;
        document.documentElement.dataset.theme = resolved;
        document.documentElement.style.colorScheme = resolved;
    }

    function setPreference(preference) {
        const value = preference === "dark" || preference === "light" ? preference : "system";
        if (value === "system") {
            localStorage.removeItem(storageKey);
        } else {
            localStorage.setItem(storageKey, value);
        }

        apply(value);
    }

    media.addEventListener("change", () => {
        if (getPreference() === "system") {
            apply("system");
        }
    });

    document.addEventListener("DOMContentLoaded", () => {
        window.Blazor?.addEventListener("enhancedload", () => apply(getPreference()));
    });

    window.dashboardTheme = {
        get: getPreference,
        set: setPreference,
        apply: () => apply(getPreference())
    };

    apply(getPreference());
})();
