/**
 * Theme Context
 * Provides theme state (light, dark, or the grayscale "gray" theme) and a setter, persisted
 * across sessions. Each non-light theme is a class on <html> (see index.css); index.html applies
 * the stored one before React loads to avoid a flash.
 */

import { createContext, useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'amr-theme';

// Order the nav toggle cycles through
// eslint-disable-next-line react-refresh/only-export-components
export const THEMES = ['light', 'dark', 'gray'];

// eslint-disable-next-line react-refresh/only-export-components
export const ThemeContext = createContext(null);

const getSystemTheme = () =>
  window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';

const getInitialTheme = () => {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (THEMES.includes(stored)) return stored;
  } catch {
    // localStorage unavailable (private browsing, etc.) - fall through
  }
  return getSystemTheme();
};

export const ThemeProvider = ({ children }) => {
  const [theme, setThemeState] = useState(getInitialTheme);

  useEffect(() => {
    const root = document.documentElement.classList;
    root.toggle('dark', theme === 'dark');
    root.toggle('gray', theme === 'gray');
  }, [theme]);

  // Follow the OS preference until the user makes an explicit choice
  useEffect(() => {
    if (localStorage.getItem(STORAGE_KEY)) return;

    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = (e) => setThemeState(e.matches ? 'dark' : 'light');
    media.addEventListener('change', handleChange);
    return () => media.removeEventListener('change', handleChange);
  }, []);

  const setTheme = useCallback((next) => {
    setThemeState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // ignore write failures, theme still applies for this session
    }
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme(THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length]);
  }, [theme, setTheme]);

  const value = { theme, setTheme, toggleTheme };

  return (
    <ThemeContext.Provider value={value}>
      {children}
    </ThemeContext.Provider>
  );
};

export default ThemeContext;
