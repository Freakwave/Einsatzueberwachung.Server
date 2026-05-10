window.hilfeDrawer = {
    scrollToSection(id) {
        const body = document.getElementById('hilfeDrawerBody');
        const el = document.getElementById(id);
        if (body && el) {
            body.scrollTo({ top: el.offsetTop - 8, behavior: 'smooth' });
        }
    }
};
