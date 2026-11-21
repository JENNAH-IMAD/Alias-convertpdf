'use client';
import Link from 'next/link';
import { flushSync } from 'react-dom';
import { DropdownMenu } from 'radix-ui';
import { ChevronDown, CircleUserRound, LoaderCircle, LogOut, Moon, Sun, UserRound } from 'lucide-react';
import { useTheme } from './theme-context';
import { useAuth } from './auth-context';

export function ThemeToggle() {
  const { setTheme, isLightMode } = useTheme();
  const nextTheme = isLightMode ? 'dark' : 'light';
  const toggleTheme = (button: HTMLButtonElement) => {
    const root = document.documentElement;
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const startTransition = document.startViewTransition?.bind(document);
    if (!startTransition || reducedMotion) {
      setTheme(nextTheme);
      return;
    }
    const bounds = button.getBoundingClientRect();
    root.style.setProperty('--theme-reveal-x', `${bounds.left + bounds.width / 2}px`);
    root.style.setProperty('--theme-reveal-y', `${bounds.top + bounds.height / 2}px`);
    root.setAttribute('data-theme-transition', '');
    try {
      const transition = startTransition(() => {
        flushSync(() => setTheme(nextTheme));
        root.setAttribute('data-theme', nextTheme);
      });
      const cleanup = () => {
        root.removeAttribute('data-theme-transition');
        root.style.removeProperty('--theme-reveal-x');
        root.style.removeProperty('--theme-reveal-y');
      };
      void transition.finished.then(cleanup, cleanup);
    } catch {
      root.removeAttribute('data-theme-transition');
      root.style.removeProperty('--theme-reveal-x');
      root.style.removeProperty('--theme-reveal-y');
      setTheme(nextTheme);
    }
  };
  return (
    <button
      type="button"
      className="alias-menu-trigger mini-menu-trigger appearance-toggle"
      aria-label={`Activer le mode ${isLightMode ? 'sombre' : 'clair'}`}
      title={`Activer le mode ${isLightMode ? 'sombre' : 'clair'}`}
      onClick={(event) => toggleTheme(event.currentTarget)}
    >
      <span className="appearance-toggle-glyph" aria-hidden="true">
        <Moon className="appearance-moon" size={17} />
        <Sun className="appearance-sun" size={17} />
      </span>
    </button>
  );
}

export function ThemeMenu() {
  const { user, logout, loggingOut, logoutError } = useAuth();
  const initials = user?.name.trim().split(/\s+/).slice(0, 2).map((part) => part.charAt(0).toLocaleUpperCase()).join('');
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        type="button"
        className="alias-menu-trigger mini-menu-trigger account-menu-trigger"
        aria-label={`Ouvrir le menu de ${user?.name || 'mon compte'}`}
        title={user?.name || 'Mon compte'}
      >
        <span className="account-menu-avatar" aria-hidden="true">
          {initials || <CircleUserRound size={17} />}
        </span>
        <span className="account-menu-name">{user?.name || 'Mon compte'}</span>
        <ChevronDown size={12} aria-hidden="true" />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          className="alias-theme-menu mini-theme-menu"
          aria-label="Menu du compte"
          align="end"
          sideOffset={8}
          collisionPadding={12}
        >
          {user && (
            <DropdownMenu.Label className="account-menu-identity">
              <strong>{user.name}</strong>
              <span>{user.email}</span>
            </DropdownMenu.Label>
          )}
          <DropdownMenu.Separator className="alias-menu-separator" />
          <DropdownMenu.Item asChild className="account-menu-item">
            <Link href="/profile">
              <UserRound size={16} aria-hidden="true" />
              Gérer mon profil
            </Link>
          </DropdownMenu.Item>
          {user && (
            <>
              <DropdownMenu.Separator className="alias-menu-separator" />
              <DropdownMenu.Item
                className="mini-logout-item"
                disabled={loggingOut}
                onSelect={(event) => {
                  event.preventDefault();
                  void logout();
                }}
              >
                {loggingOut ? (
                  <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />
                ) : (
                  <LogOut size={16} aria-hidden="true" />
                )}
                {loggingOut ? 'Déconnexion en cours' : 'Se déconnecter'}
              </DropdownMenu.Item>
              {logoutError && (
                <p role="alert" className="mini-menu-error">
                  {logoutError}
                </p>
              )}
            </>
          )}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
