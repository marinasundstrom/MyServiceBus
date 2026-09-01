(() => {
    const storageKey = "myservicebus.dashboard.density";

    function getPreference() {
        return localStorage.getItem(storageKey) === "compact" ? "compact" : "comfortable";
    }

    function apply(preference) {
        document.documentElement.dataset.density = preference === "compact" ? "compact" : "comfortable";
    }

    function setPreference(preference) {
        const value = preference === "compact" ? "compact" : "comfortable";
        if (value === "comfortable") {
            localStorage.removeItem(storageKey);
        } else {
            localStorage.setItem(storageKey, value);
        }

        apply(value);
    }

    document.addEventListener("DOMContentLoaded", () => {
        window.Blazor?.addEventListener("enhancedload", () => apply(getPreference()));
    });

    window.dashboardDensity = {
        get: getPreference,
        set: setPreference,
        apply: () => apply(getPreference())
    };

    apply(getPreference());
})();
