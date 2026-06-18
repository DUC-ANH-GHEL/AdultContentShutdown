# Huong Dan Publish Len Microsoft Edge Add-ons

Tai lieu nay dung cho extension `Adult Content Shutdown Guard`.

## Trang Thai Source Da Chuan Bi

- Manifest V3.
- Metadata khong con mojibake.
- Co icon PNG `16/32/48/128`.
- Co `schema.json` cho managed storage.
- Token khong hardcode trong package. Installer se ghi token vao Edge managed storage policy sau khi co extension ID tren store.

## 1. Tao Tai Khoan Developer

1. Mo Partner Center: `https://partner.microsoft.com/dashboard`.
2. Dang nhap bang Microsoft account ca nhan, vi Edge program can MSA lam primary owner.
3. Vao Edge program va hoan tat dang ky.
4. Microsoft docs hien ghi Edge extension program khong co phi dang ky.

## 2. Dong Goi Extension

Chay tu root repo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-edge-extension.ps1
```

File upload se nam o:

```text
dist\edge-addons\AdultContentShutdownGuard-edge-1.0.1.zip
```

Khong upload `.crx`, `.pem`, hoac thu muc `C:\ProgramData\AdultContentShutdownGuard`.

## 3. Tao Extension Moi Trong Partner Center

1. Vao Partner Center > Edge.
2. Chon `Create new extension`.
3. Upload file zip trong `dist\edge-addons`.
4. Neu validation fail, sua source va chay lai package script.

## 4. Availability

Khuyen nghi ban dau:

- Visibility: `Hidden`, neu chi dung noi bo/gia dinh va khong muon public search.
- Markets: chon thi truong ban can, hoac de default all markets.

Sau khi hidden extension duoc approve, ban van co listing URL va extension ID de policy force-install.

## 5. Properties

Gia tri goi y:

- Category: `Productivity` hoac `Accessibility`.
- Mature content: `No`. Extension xu ly enforcement noi dung nguoi lon, nhung package khong chua mature content.
- Website: URL project/support cua ban neu co.
- Support contact: email ho tro cua ban.
- Privacy policy requirements: chon `Yes`, vi extension doc URL/title/noi dung trang va gui ve service local.
- Privacy policy URL: dung URL public chua noi dung trong `docs/privacy-policy-edge-addons-template.md`.

## 6. Privacy Form

Single purpose:

```text
This extension connects Microsoft Edge to a locally installed Windows service controlled by the device administrator. It checks visited pages against adult-content rules and reports confirmed violations to the local service for enforcement.
```

Remote code:

```text
No. The extension does not execute remotely hosted code. It only sends local HTTP requests to http://127.0.0.1:8765 when configured by administrator policy.
```

Data usage:

```text
The extension reads page URL, hostname, title, meta tags, headings, and limited visible text only to evaluate adult-content risk. Confirmed violations are sent to a local Windows service on the same device. The extension does not send browsing data to external servers.
```

Permission justification:

- `tabs`: needed to read active tab URL/title and evaluate completed navigations.
- `webNavigation`: needed to trigger checks when top-level page navigation completes.
- `storage`: needed to read administrator-managed service URL and token from managed storage policy.
- `<all_urls>`: needed because adult-content risk can appear on arbitrary websites.
- `http://127.0.0.1:8765/*`: needed to communicate with the local Windows service.

## 7. Store Listing

Dung copy trong `docs/store-listing-edge-addons.md`.

Can co:

- Extension logo: dung `browser-extension/icons/icon128.png`.
- Screenshots: chup popup extension va trang health/service status neu can.
- Short description: lay tu file store listing.

## 8. Submit Review

1. Dien certification notes trong `docs/store-listing-edge-addons.md`.
2. Submit.
3. Doi review. Neu fail, doc reason va sua package/listing/privacy policy.

## 9. Sau Khi Duoc Duyet

Lay extension ID tren Partner Center hoac listing URL.

Chay PowerShell Admin:

```powershell
cd D:\AdultContentShutdownGuard
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\configure-edge-addons-extension.ps1 -EdgeExtensionId <EDGE_EXTENSION_ID>
```

Sau do:

1. Mo `edge://policy`.
2. Bam `Tai lai chinh sach`.
3. Kiem tra `ExtensionInstallForcelist` va `ExtensionSettings` khong con `[BLOCKED]`.
4. Dong/mo lai Edge neu extension chua tu cai.
5. Kiem tra folder extension:

```powershell
$id = '<EDGE_EXTENSION_ID>'
Test-Path "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Extensions\$id"
```

## 10. Cap Nhat Version Sau Nay

1. Tang `version` trong `browser-extension/manifest.json`.
2. Chay package script.
3. Upload zip moi vao Partner Center.
4. Doi review update.

Khong doi token trong package. Token la policy rieng cua tung may, do installer/configure script ghi vao registry.
