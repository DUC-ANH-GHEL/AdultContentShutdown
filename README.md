# AdultContentShutdownGuard

AdultContentShutdownGuard la he thong parental-control chay cuc bo tren Windows cho may do chu so huu hoac quan tri vien quan ly. Ban hien tai uu tien safe-mode: service khong tu doi DNS adapter, khong tao firewall rule, khong sua registry browser policy, va khong bind DNS port `53` theo mac dinh.

Extension Chrome/Edge trong `browser-extension` la lop browser-managed de bat domain/page state ngay ca khi VPN che DNS. Service van co the hoat dong doc lap bang passive DNS monitoring va process bypass monitoring, nhung de strict qua VPN can extension duoc force-install bang browser policy.

## Kien Truc Mac Dinh

Windows DNS Client event log -> Passive DNS monitor -> blocklist local/remote -> log/shutdown

Service giu health endpoint tren `127.0.0.1:8765`. Neu bat blocklist remote, service chi tai danh sach domain tu HTTPS URL cau hinh va bat buoc kiem tra SHA-256 truoc khi ghi cache.

## Chuc Nang

- Passive DNS monitor doc Windows DNS Client operational events de phat hien domain bi chan ma khong doi DNS cua may.
- Managed browser endpoint nhan domain tu extension Chrome/Edge da duoc quan ly, giup thay domain ngay ca khi VPN che DNS khoi Windows event log.
- Process monitor phat hien VPN/proxy/Tor va cac bypass process trong danh sach cau hinh.
- Network posture monitor chi log rui ro nhu DoH policy chua khoa, DNS adapter khong do guard quan ly, firewall rule chua cai. Mac dinh khong tu sua.
- DNS sinkhole local van ton tai nhu che do opt-in thu cong bang `Dns.Enabled=true`.
- Browser policy/firewall/DNS hardening van ton tai nhu che do opt-in, nhung mac dinh tat.
- Legacy extension endpoint `/violation` mac dinh tat.
- Log JSON line tai may local.

## Gioi Han Ky Thuat

- Passive mode khong doc duoc noi dung body trang HTTPS.
- Neu browser dung VPN va DNS khong lo qua Windows DNS Client, passive DNS co the khong thay domain that. De bat qua VPN can managed browser extension hoac mot diem quan sat tu trong browser.
- Neu nguoi dung hang ngay co quyen Administrator, ho van co the go service hoac sua cau hinh he thong. Muon strict cap enterprise can MDM, AppLocker/WDAC hoac co che quan tri thiet bi tuong duong.

## Build

```powershell
dotnet restore
dotnet build
dotnet test
powershell -ExecutionPolicy Bypass -File .\scripts\publish-service.ps1
```

## Cai Windows Service

Mo PowerShell voi quyen Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

Script cai service va recovery, tao thu muc log/config. Safe-mode mac dinh khong doi DNS, firewall hoac browser policy.

Installer se sinh token ngau nhien cho endpoint browser-managed va copy extension local co token vao:

```text
C:\ProgramData\AdultContentShutdownGuard\browser-extension
```

Installer sinh token ngau nhien cho endpoint browser-managed va ghi token vao Chrome/Edge managed storage policy. Extension publish len store khong hardcode token.

Ban self-host CRX cuc bo co the bi Edge/Chrome tren may unmanaged chan (`[BLOCKED]`). Flow production khuyen nghi la publish len Microsoft Edge Add-ons, lay extension ID, roi chay:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\configure-edge-addons-extension.ps1 -EdgeExtensionId <EDGE_EXTENSION_ID>
```

Script nay cung ghi Edge policy `MandatoryExtensionsForInPrivateNavigation`. InPrivate van duoc mo, nhung Edge se chan duyet InPrivate neu extension chua duoc allow trong InPrivate; user thuong khong the tat extension da force-install de bypass policy nay. Voi Chrome, script ghi `IncognitoModeAvailability=1` de tat han Incognito vi Chrome khong cho admin tu dong bat extension trong Incognito.

Tai lieu chi tiet nam tai `docs/edge-addons-publish-guide.md`.

### Bao Ve Goi Cai Dat / Tat Extension

Installer tao secret cuc bo tai `C:\ProgramData\AdultContentShutdownGuard\Security\uninstall-secret.bin` va khoa ACL chi cho `Administrators`/`SYSTEM` doc. Ma go cai dat duoc tinh tu secret nay va tu dong doi theo gio.

Lay ma hien tai bang PowerShell Admin:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\show-uninstall-code.ps1
```

Tat managed browser guard policy hop le:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\disable-managed-browser-guard.ps1 -UninstallCode <MA_HIEN_TAI>
```

Go service hop le:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-service.ps1 -UninstallCode <MA_HIEN_TAI>
```

Nguoi dung thuong khong co quyen doc secret hoac sua HKLM policy. Neu user co Administrator, Windows app khong the ngan bypass tuyet doi; luc do can MDM/Intune/AppLocker/WDAC de khoa may o cap thiet bi.

Health endpoint:

```text
http://127.0.0.1:8765/health
```

## Kiem Tra An Toan

Neu test tren may that, nen dat:

```json
"DryRun": true
```

Khi `DryRun=true`, service chi log. Khi `DryRun=false`, `BlockedDomain` hoac bypass process co the goi:

```text
shutdown.exe /s /t 0 /f /c "Adult content blocked by AdultContentShutdownGuard"
```

## Cau Hinh Chinh

- `DryRun`: `false` cho production, `true` de test khong tat may.
- `Dns.Enabled`: mac dinh `false`. Bat `true` la che do DNS sinkhole opt-in, co the anh huong browsing neu service loi.
- `Enforcement.ApplyOnStartup`: mac dinh `false`; neu bat thi service moi apply DNS/firewall hardening.
- `Enforcement.ConfigureDnsAdapters`: mac dinh `false`.
- `Enforcement.ConfigureFirewallRules`: mac dinh `false`.
- `BrowserPolicies.Enabled`: mac dinh `false`; neu bat thi service moi ghi registry browser policy.
- `Tamper.RestoreSettings`: mac dinh `false`; safe-mode khong tu restore cau hinh he thong.
- `PassiveDnsMonitor.Enabled`: mac dinh `true`.
- `NetworkPosture.ActionOnUnsafePosture`: mac dinh `LogOnly`, khong shutdown chi vi DNS/firewall/policy chua duoc enforce.
- `ProcessRules.AllowedWorkVpnProcesses`: VPN cong viec duoc log-only, khong shutdown mac dinh.
- `ProcessRules.ActionOnWorkVpnDetected`: mac dinh `LogOnly`.
- `ProcessRules.BlockedProcessNames`: Tor/proxy bypass bi coi la vi pham nghiem trong.
- `ManagedBrowserEndpoint.Enabled`: source mac dinh `false`; installer bat `true` va sinh token ngau nhien tren ban da cai.
- `ManagedBrowserEndpoint.ChromeExtensionId` / `EdgeExtensionId`: ID extension da publish/pack de force-install.
- `ManagedBrowserEndpoint.UpdateUrl`: update manifest URL cho Chrome/Edge force-install.
- `BlocklistUpdates.RemoteUrl`: HTTPS URL tai blocklist ngoai.
- `BlocklistUpdates.Sha256`: SHA-256 bat buoc cua remote blocklist. Neu trong hoac khong khop, remote list bi tu choi va service dung local/cache cu.
- `LegacyExtensionEndpointEnabled`: mac dinh `false`; chi bat neu van dung extension legacy va da thay `Token`.
- `Token`: chi dung cho endpoint extension legacy. Placeholder `CHANGE_THIS_SECRET_TOKEN` luon bi tu choi.

## Dong Goi Edge Add-ons

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-edge-extension.ps1
```

File upload nam trong `dist\edge-addons`. Khong upload `.crx` hoac `.pem` len Edge Add-ons; store can `.zip` chua manifest, JS, schema va icons.

Blocklist local nam tai `src/Guard.Service/Config/adult-domains.txt`.

## Che Do Strict Opt-in

Neu chap nhan tac dong toi cau hinh mang, admin co the bat:

```json
"Dns": { "Enabled": true },
"Enforcement": {
  "ApplyOnStartup": true,
  "ConfigureDnsAdapters": true,
  "ConfigureFirewallRules": true
},
"BrowserPolicies": { "Enabled": true },
"Tamper": { "RestoreSettings": true }
```

Che do nay co the doi DNS adapter ve `127.0.0.1`, tao firewall rule va ghi registry policy. Nen test trong VM hoac may phu truoc.

## Go Cai Dat

Mo PowerShell voi quyen Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-service.ps1 -UninstallCode <MA_HIEN_TAI>
```

Script go service va xoa firewall rule co prefix `AdultContentShutdownGuard*` neu ton tai. Script khong xoa browser policy cua nguoi dung trong safe-mode. Log chi bi xoa neu ban xac nhan.

## Cau Truc Repository

- `src/Guard.Service`: Windows Service, passive DNS monitor, opt-in DNS resolver, enforcement, monitor va logger.
- `browser-extension`: extension browser-managed de publish len Edge Add-ons/Chrome Web Store.
- `docs`: huong dan publish Edge Add-ons, privacy policy template va store listing copy.
- `scripts`: publish, install va uninstall.
- `tests/Guard.Service.Tests`: unit test cho safe defaults, passive DNS parser, domain matching va DNS packet behavior.
- `tests/adult-test-page.html`: trang test legacy cho extension.
