# Privacy Policy Template For Edge Add-ons

Replace bracketed values before publishing.

## Adult Content Shutdown Guard Privacy Policy

Effective date: [DATE]

Adult Content Shutdown Guard is a Microsoft Edge extension used with a locally installed Windows service on a device controlled by the device owner or administrator.

## Data The Extension Reads

The extension may read the current page URL, hostname, title, meta description, meta keywords, headings, and a limited amount of visible page text. This data is used only to evaluate whether a page matches adult-content rules.

## Data The Extension Sends

When a page reaches the configured violation threshold, the extension sends the URL, hostname, title, matched rule summary, and detection timestamp to a local Windows service at `http://127.0.0.1:8765` on the same device.

The extension does not send browsing data to external servers operated by [PUBLISHER NAME].

## Local Enforcement

The local Windows service may log events locally and may perform the configured enforcement action, including shutting down the device when the administrator enables that behavior.

## Data Retention

Event logs are stored locally on the device under the Windows service log directory. [Describe how long logs are kept or how the administrator can delete them.]

## Third Parties

The extension does not sell user data and does not share browsing data with third-party analytics or advertising services.

## User Controls

This extension is intended for administrator-managed devices. The administrator controls installation, policy settings, and removal.

## Contact

For privacy questions, contact: [SUPPORT EMAIL OR URL]
