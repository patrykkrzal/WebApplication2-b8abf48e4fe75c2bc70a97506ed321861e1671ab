(function(window){
 window.__rent = window.__rent || {};
 const apiBase = window.__rent.apiBase || '';
 // helper wrappers
 async function apiFetch(url, opts = {}){
 const full = (url.startsWith('http') || url.startsWith('/')) ? url : (apiBase.replace(/\/$/, '') + '/' + url.replace(/^\//, ''));
 const merged = Object.assign({ credentials: 'include' }, opts);
 const res = await fetch(full, merged);
 return res;
 }
 async function apiGet(url){ const r = await apiFetch(url, { method: 'GET' }); if (!r.ok) throw new Error(await r.text()); return r.json(); }
 async function apiPost(url, body){ const r = await apiFetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); const txt = await r.text(); try{ return JSON.parse(txt);}catch{return txt;} }
 async function apiDelete(url, body){ const r = await apiFetch(url, { method: 'DELETE', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); const txt = await r.text(); try{ return JSON.parse(txt);}catch{return txt;} }
 window.__rent.api = { get: apiGet, fetch: apiFetch, post: apiPost, del: apiDelete };

 // auth/init logic extracted from _Layout
 async function getMe(){ try{
 let res = await apiFetch('/api/auth/me'); if (!res.ok) res = await apiFetch('/api/users/me'); if (!res.ok) return null; return await res.json(); } catch { return null; }
 }
 async function initAuth(){
 const authState = { user: null, roles: [], isAuthenticated: false };
 const me = await getMe();
 if (me && (Array.isArray(me.Roles) || Array.isArray(me.roles))){ authState.user = me; authState.roles = (me.Roles || me.roles).map(r => String(r).toLowerCase()); authState.isAuthenticated = true; }
 window.auth = {
 get user(){ return authState.user; },
 get roles(){ return authState.roles; },
 get isAuthenticated(){ return authState.isAuthenticated; },
 hasAnyRole: (...roles) => authState.isAuthenticated && roles.map(r => String(r).toLowerCase()).some(r => authState.roles.includes(r))
 };
 // apply role vis (for initial render) - pages can call applyRoleVisibility() if they need immediate changes
 window.__rent.applyRoleVisibility = function(){
 document.querySelectorAll('[data-show-when-auth="true"]').forEach(el => el.style.display = authState.isAuthenticated ? '' : 'none');
 document.querySelectorAll('[data-hide-when-auth="true"]').forEach(el => el.style.display = authState.isAuthenticated ? 'none' : '');
 document.querySelectorAll('[data-show-for-roles]').forEach(el => {
 const rolesAttr = el.getAttribute('data-show-for-roles') || '';
 const allowed = rolesAttr.split(',').map(r => r.trim().toLowerCase()).filter(Boolean);
 el.style.display = (authState.isAuthenticated && allowed.some(r => authState.roles.includes(r))) ? '' : 'none';
 });
 document.querySelectorAll('[data-hide-for-roles]').forEach(el => {
 const rolesAttr = el.getAttribute('data-hide-for-roles') || '';
 const hideFor = rolesAttr.split(',').map(r => r.trim().toLowerCase()).filter(Boolean);
 const shouldHide = authState.isAuthenticated && hideFor.some(r => authState.roles.includes(r));
 if (shouldHide) el.style.display = 'none';
 });
 // handle .user-panel enabling/disabling explicitly
 document.querySelectorAll('.user-panel').forEach(el => {
 const summary = el.querySelector('summary.winter-button');
 if (authState.isAuthenticated) {
 el.classList.remove('disabled');
 try { el.removeAttribute('aria-disabled'); el.style.pointerEvents = 'auto'; } catch(e){}
 if (summary) { try { summary.style.pointerEvents = 'auto'; summary.style.cursor = 'pointer'; } catch(e){} }
 } else {
 el.classList.add('disabled');
 try { el.setAttribute('aria-disabled','true'); el.style.pointerEvents = 'none'; } catch(e){}
 if (summary) { try { summary.style.pointerEvents = 'none'; summary.style.cursor = 'default'; } catch(e){} }
 }
 });
 // update login button
 const loginBtn = document.getElementById('loginBtn');
 if (loginBtn){
 if (authState.isAuthenticated){
 loginBtn.textContent = 'Wyloguj'; loginBtn.href = '#'; loginBtn.onclick = async (e) => { e.preventDefault(); try{ const res = await apiFetch('/api/auth/logout', { method: 'POST' }); // re-init auth and update UI
 await initAuth(); window.__rent.applyRoleVisibility(); // redirect to home so user lands on main page after logout
 // prefer root, but handle case where static files served under different base
 const base = window.__rent && window.__rent.apiBase ? window.__rent.apiBase : '/';
 window.location.href = base === '' ? '/' : base;
 } catch { window.location.href = '/'; } };
 } else { loginBtn.textContent = 'Logowanie'; loginBtn.href = '/Login.html'; loginBtn.onclick = null; }
 }
 // worker info
 try{ const workerInfoEl = document.getElementById('workerInfo'); if (workerInfoEl && authState.isAuthenticated && authState.roles.length ===1 && authState.roles.includes('worker')){ const u = authState.user || {}; const job = u.Job_Title || u.JobTitle || u.jobTitle || u.job_title || u.Job || ''; const start = u.WorkStart || u.Work_Start || u.workStart || u.WorkStartTime || ''; const end = u.WorkEnd || u.Work_End || u.workEnd || u.WorkEndTime || ''; const days = u.Working_Days || u.WorkingDays || u.working_days || u.Working_Days || ''; const parts = []; if (job) parts.push(`<div><strong>Stanowisko:</strong> ${job}</div>`); if (start || end) parts.push(`<div><strong>Godziny pracy:</strong> ${start || '?'} — ${end || '?'}</div>`); if (days) parts.push(`<div><strong>Dni pracy:</strong> ${days}</div>`); if (parts.length) { workerInfoEl.innerHTML = parts.join(''); workerInfoEl.style.display = 'block'; } } } catch(e){}
 // dispatch event so pages can react
 try{ document.dispatchEvent(new CustomEvent('auth-changed', { detail: { isAuthenticated: authState.isAuthenticated, roles: authState.roles, user: authState.user } })); } catch(e){}
 };
 // call apply so UI reflects updated auth state immediately
 try { window.__rent.applyRoleVisibility(); } catch(e) { /* ignore */ }
 }
 // run on DOMContentLoaded
 document.addEventListener('DOMContentLoaded', () => initAuth());
 // expose helpers
 window.__rent.getMe = getMe;
 window.__rent.initAuth = initAuth;
 })(window);
