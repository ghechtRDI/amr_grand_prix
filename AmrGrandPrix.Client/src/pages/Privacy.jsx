/**
 * Privacy Page (public)
 * What the site collects and why. Keep the analytics section in step with lib/analytics.js.
 */

import { Link } from 'react-router-dom';

const LAST_UPDATED = 'November 25, 2026';

function Section({ title, children }) {
  return (
    <section className="mt-8">
      <h2 className="text-lg font-semibold">{title}</h2>
      <div className="mt-2 flex flex-col gap-3 text-muted-foreground">{children}</div>
    </section>
  );
}

const linkClass = 'font-medium text-primary hover:underline';

export default function Privacy() {
  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <article className="mx-auto max-w-3xl leading-relaxed">
        <div className="mb-2 text-sm text-muted-foreground">
          <Link to="/" className="hover:text-foreground">Home</Link>
          <span className="mx-1.5">/</span>
          <span>Privacy</span>
        </div>
        <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
          Privacy
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">Last updated {LAST_UPDATED}</p>

        <Section title="Race results">
          <p>
            This site publishes results from Alaska mountain and trail races: each finisher&apos;s name, time,
            place, age group, and gender as reported by the race, plus Grand Prix points and standings
            calculated from them. Results are public and can be viewed without an account.
          </p>
          <p>
            Profile pages of runners under 18, or whose age we don&apos;t know, are kept out of search engines.
            Their results still appear on race and standings pages.
          </p>
          <p>
            If something about you is wrong, or you&apos;d like a correction, please{' '}
            <Link to="/report" className={linkClass}>report an issue</Link>.
          </p>
        </Section>

        <Section title="Accounts">
          <p>
            Creating an account is optional. If you register, we store your email address, name, and the
            optional details you provide (preferred name, other names you&apos;ve raced under, hometown, and
            date of birth). We use them to sign you in, to match you with your results, and to email you
            about your account. Your email address and date of birth are never shown publicly.
          </p>
          <p>
            When you sign in, your browser stores a sign-in token so you stay signed in. Your light/dark
            theme choice is stored in your browser too.
          </p>
        </Section>

        <Section title="Analytics">
          <p>
            We use Google Analytics to understand how the site is used: which pages are viewed, roughly
            where visitors are (city or region), what kind of device and browser they use, and how they
            found the site. Google Analytics sets cookies in your browser to tell visits apart. We see
            these numbers in aggregate, not who you are.
          </p>
          <p>
            We also count a few actions, such as changing the standings view, using the athlete search,
            claiming results, and registering. We record only that the action happened: what you type
            into a search box is never sent.
          </p>
          <p>
            Google signals and ad personalization are turned off, and the data isn&apos;t used for
            advertising. Google processes it under its own{' '}
            <a href="https://policies.google.com/privacy" className={linkClass} target="_blank" rel="noopener noreferrer">
              privacy policy
            </a>
            . To opt out, you can use Google&apos;s{' '}
            <a href="https://tools.google.com/dlpage/gaoptout" className={linkClass} target="_blank" rel="noopener noreferrer">
              opt-out browser add-on
            </a>{' '}
            or a content blocker.
          </p>
        </Section>

        <Section title="Spam protection">
          <p>
            The registration and report forms use Cloudflare Turnstile to check that a person, not a bot,
            is submitting them.
          </p>
        </Section>

        <Section title="Questions">
          <p>
            This site is run by volunteers for Alaska Mountain Runners. For questions about this policy,
            please <Link to="/report" className={linkClass}>contact us</Link>.
          </p>
        </Section>
      </article>
    </div>
  );
}
