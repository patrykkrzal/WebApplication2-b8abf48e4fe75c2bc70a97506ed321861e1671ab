(function () {

    function go(url) {
        if (!url) return;
        // if relative without protocol and without leading slash -> make it absolute from site root
        if (!/^(https?:)?\/\//i.test(url) && url.charAt(0) !== '/') {
            url = '/' + url.replace(/^\.?\//, '');
        }
        window.location.href = url;
    }

    function bindNav() {
        var nodes = document.querySelectorAll('[data-nav]');
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].addEventListener('click', function () {
                go(this.getAttribute('data-nav'));
            });
        }
    }

    const authState = { user: null, roles: [], isAuthenticated: false };

    // Ukrycie elementów zanim stan auth bêdzie znany + zablokowanie panelu
    function preHideAuthDependent() {
        document.querySelectorAll('[data-show-when-auth="true"]').forEach(el => {
            el.style.display = 'none';
        });
        // domyœlnie panel u¿ytkownika ma byæ nieklikalny zanim poznamy auth
        setUserPanelDisabled(true);
    }

    async function getMe() {
        try {
            let res = await fetch('/api/auth/me', { credentials: 'include' });
            if (!res.ok) {
                res = await fetch('/api/users/me', { credentials: 'include' });
            }
            if (!res.ok) return null;
            return await res.json();
        } catch {
            return null;
        }
    }

    async function logout() {
        try {
            await fetch('/api/auth/logout', {
                method: 'POST',
                credentials: 'include'
            });
        } catch { }

        authState.user = null;
        authState.roles = [];
        authState.isAuthenticated = false;
        applyRoleVisibility();

        window.location.href = '/Index.html';
    }

    function setUserPanelDisabled(disabled) {
        document.querySelectorAll('.user-panel').forEach(el => {
            el.classList.toggle('disabled', disabled);
            // jeœli disabled, upewnij siê ¿e panel jest zamkniêty
            if (disabled && el.open) el.open = false;
        });
    }

    // Blokada otwierania <details> gdy ma klasê disabled
    function enforceUserPanelGuard() {
        document.querySelectorAll('details.user-panel').forEach(d => {
            // zamknij jeœli disabled
            if (d.classList.contains('disabled') && d.open) d.open = false;

            // na ka¿dy toggle wymuœ zamkniêcie gdy disabled
            d.addEventListener('toggle', () => {
                if (d.classList.contains('disabled') && d.open) {
                    d.open = false;
                }
            });

            // przechwyæ klikniêcia, aby nie otwieraæ gdy disabled
            d.addEventListener('click', (e) => {
                if (d.classList.contains('disabled')) {
                    e.preventDefault();
                    e.stopPropagation();
                }
            });

            // przechwyæ klawisze na summary (Enter/Space)
            const summary = d.querySelector('summary');
            if (summary) {
                summary.addEventListener('keydown', (e) => {
                    if (d.classList.contains('disabled') && (e.key === 'Enter' || e.key === ' ')) {
                        e.preventDefault();
                        e.stopPropagation();
                    }
                });
            }
        });
    }

    function applyRoleVisibility() {

        // elementy widoczne na podstawie ról
        const nodes = document.querySelectorAll('[data-required-roles]');
        nodes.forEach(el => {
            const rolesAttr = (el.getAttribute('data-required-roles') || '').trim();
            if (!rolesAttr) return;

            const required = rolesAttr
                .split(',')
                .map(s => s.trim().toLowerCase())
                .filter(Boolean);

            const ok = authState.isAuthenticated &&
                required.some(r => authState.roles.includes(r));

            el.style.display = ok ? '' : 'none';
        });

        // elementy widoczne tylko gdy NIEzalogowany
        document.querySelectorAll('[data-hide-when-auth="true"]').forEach(el => {
            el.style.display = authState.isAuthenticated ? 'none' : '';
        });

        // elementy widoczne tylko gdy zalogowany
        document.querySelectorAll('[data-show-when-auth="true"]').forEach(el => {
            el.style.display = authState.isAuthenticated ? '' : 'none';
        });

        // przycisk wylogowania
        document.querySelectorAll('[data-logout="true"]').forEach(el => {
            el.onclick = logout;
        });

        // panel u¿ytkownika
        setUserPanelDisabled(!authState.isAuthenticated);
        enforceUserPanelGuard();
    }

    async function initAuth() {
        const me = await getMe();
        if (me && (Array.isArray(me.Roles) || Array.isArray(me.roles))) {
            const roles = (me.Roles || me.roles)
                .map(r => String(r).toLowerCase());

            authState.user = me;
            authState.roles = roles;
            authState.isAuthenticated = true;
        }

        // globalny obiekt
        window.auth = {
            get user() { return authState.user; },
            get roles() { return authState.roles; },
            get isAuthenticated() { return authState.isAuthenticated; },
            hasAnyRole: (...roles) =>
                authState.isAuthenticated &&
                roles.map(r => String(r).toLowerCase())
                    .some(r => authState.roles.includes(r))
        };

        applyRoleVisibility();
    }

    document.addEventListener('DOMContentLoaded', () => {
        preHideAuthDependent();
        bindNav();
        enforceUserPanelGuard();
        initAuth();
    });

})();
