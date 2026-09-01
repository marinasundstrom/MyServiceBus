window.dashboardNavigation = {
    revealActive: () => {
        const navigation = document.querySelector(".sidebar nav");
        const active = navigation?.querySelector("a.active");
        if (!navigation || !active || navigation.scrollWidth <= navigation.clientWidth)
            return;

        const left = active.offsetLeft - (navigation.clientWidth - active.offsetWidth) / 2;
        navigation.scrollTo({ left: Math.max(0, left) });
    }
};
