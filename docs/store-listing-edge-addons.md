# Store Listing Copy For Microsoft Edge Add-ons

## Extension Name

Adult Content Shutdown Guard

## Short Description

Connects Microsoft Edge to a local Windows service for administrator-managed adult-content enforcement.

## Description

Adult Content Shutdown Guard is designed for Windows devices controlled by a device owner or administrator.

The extension connects Microsoft Edge to the local AdultContentShutdownGuard Windows service. It evaluates visited pages against adult-content rules and reports confirmed violations to the local service for enforcement.

Key points:

- Works with a local Windows service on the same device.
- Uses administrator-managed policy for deployment and configuration.
- Supports VPN workflows by observing browser page state directly instead of relying only on DNS events.
- Sends violation events only to `127.0.0.1`, not to an external cloud service.
- Intended for managed devices where the administrator has authority to enforce usage rules.

This extension is not a standalone content blocker. It must be deployed with the AdultContentShutdownGuard Windows service.

## Certification Notes

This extension requires the companion Windows service. The service listens locally on `http://127.0.0.1:8765`.

For store review, the extension can be loaded and inspected without the service. Without administrator managed storage policy, the extension does not have a service token and will not submit violation events.

The extension does not execute remote code. It reads administrator-managed configuration through `chrome.storage.managed` and sends confirmed violation events only to the local service.

## Permission Justification

`tabs`: reads tab URL/title to evaluate completed navigations.

`webNavigation`: receives top-level navigation completion events so checks occur after page load.

`storage`: reads administrator-managed local service URL and token.

`<all_urls>`: adult-content risk can appear on arbitrary domains and pages.

`http://127.0.0.1:8765/*`: communicates with the companion local Windows service.
