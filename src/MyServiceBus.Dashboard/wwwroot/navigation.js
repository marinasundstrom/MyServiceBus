const revealActiveDashboardSection = () => {
    const navigation = document.querySelector(".sidebar nav");
    const active = navigation?.querySelector("a.active");
    if (!navigation || !active || navigation.scrollWidth <= navigation.clientWidth)
        return;

    const navigationBounds = navigation.getBoundingClientRect();
    const activeBounds = active.getBoundingClientRect();
    const left = navigation.scrollLeft
        + activeBounds.left
        - navigationBounds.left
        - (navigationBounds.width - activeBounds.width) / 2;
    navigation.scrollLeft = Math.max(0, left);
};

window.dashboardNavigation = {
    revealActive: () => {
        revealActiveDashboardSection();
        window.setTimeout(revealActiveDashboardSection, 50);
        window.setTimeout(revealActiveDashboardSection, 250);
    }
};

window.addEventListener("DOMContentLoaded", () => {
    window.setTimeout(revealActiveDashboardSection, 0);
    window.setTimeout(revealActiveDashboardSection, 250);
});
