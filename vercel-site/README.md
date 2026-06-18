# Adult Content Shutdown Guard Vercel Site

Static privacy-policy site for Microsoft Edge Add-ons review.

## Local Structure

- `/` serves `index.html`.
- `/privacy/adult-content-shutdown-guard` serves `privacy/adult-content-shutdown-guard/index.html`.
- `styles.css` is shared by both pages.

## Deploy To Vercel

1. Push this repository to GitHub.
2. In Vercel, select `Add New` > `Project`.
3. Import the GitHub repository.
4. Set `Framework Preset` to `Other`.
5. Set `Root Directory` to `vercel-site`.
6. Leave `Build Command` empty.
7. Leave `Output Directory` empty or default.
8. Click `Deploy`.

After deploy, use this privacy policy URL in Microsoft Partner Center:

```text
https://<your-vercel-domain>/privacy/adult-content-shutdown-guard
```
