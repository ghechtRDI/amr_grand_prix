# Branding sources

`og-image.svg` is the source for `public/og-image.png`, the default Open Graph / Twitter
link-preview image (1200×630) the server puts on every page. Regenerate the PNG after editing it:

```bash
cd AmrGrandPrix.Client
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --hide-scrollbars \
  --window-size=1200,630 --screenshot=public/og-image.png "file://$PWD/branding/og-image.svg"
```
