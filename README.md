# Adult Content Shutdown Guard

Adult Content Shutdown Guard la dich vu bao ve cap may Windows. Du an khong dung extension trinh duyet: tat ca profile Chrome, Edge, Firefox, Guest va rieng tu deu di qua lop DNS cuc bo cua may.

## Co che bao ve

```text
Moi ung dung -> DNS IPv4/IPv6 cua Windows -> DNS cuc bo 127.0.0.1 / ::1
             -> danh sach chan tai may -> NXDOMAIN + hanh dong bao ve
```

- Dich vu DNS chay tren `127.0.0.1:53` va `::1:53`, chan ten mien nam trong danh sach tai may cho moi tai khoan Windows va moi profile trinh duyet.
- Chinh sach cap may tat DNS-over-HTTPS, QUIC, cua so rieng tu va che do Khach cua Chrome, Edge, Firefox.
- Tu dong kiem tra va khoi phuc DNS, firewall va chinh sach trinh duyet neu bi thay doi.
- Chan DNS-over-TLS va DNS truc tiep tren cac trinh duyet pho bien; dong thoi theo doi DNS thụ dong va cac tien trinh vuot qua nhu Tor/psiphon.
- Danh sach tu xa chi duoc tai qua HTTPS va phai khop SHA-256. Neu khong hop, dich vu chi dung danh sach local/cache truoc do.
- Endpoint `http://127.0.0.1:8765/health` chi de doc trang thai. Khong co endpoint nhan URL, token hay extension.

Che do mac dinh chan DNS va ghi nhat ky, khong tu tat may. `AllowMachineShutdown` mac dinh la `false` va la khoa an toan bat buoc: du cau hinh mot hanh dong la `Shutdown`, dich vu van khong goi `shutdown.exe` neu quan tri vien chua chu dong bat co nay. Khong bat co nay truoc khi hoan tat luong canh bao va kiem thu tren may phu.

## Gioi han ky thuat trung thuc

Khong phan mem Windows nao co the chan tuyet doi nguoi dang dung tai khoan **Quan tri vien** hoac nguoi co the khoi dong tu USB: ho co quyen go dich vu, sua registry va cai lai he dieu hanh. De bao ve thuc te, tai khoan su dung hang ngay phai la tai khoan thuong; mat khau Quan tri vien phai do nguoi giam ho giu. WDAC/AppLocker hoac MDM se la lop bo sung khi can khoa ca trinh duyet di dong/portable.

DNS chi nhin thay ten mien, khong doc noi dung HTTPS. Khong su dung giai ma TLS hay cai chung chi goc, vi cach do lam giam an toan va rieng tu cua tai khoan, ngan hang va dich vu ca nhan. Neu can phan tich luong ma hoa ma van khong giai ma, giai phap cap san pham la WFP callout driver da ky so; phan nay khong nen lam bang driver tu ky tren may dung that.

## Cai dat

Mo PowerShell voi quyen Quan tri vien:

```powershell
dotnet restore
dotnet test
powershell -ExecutionPolicy Bypass -File .\scripts\publish-service.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

Truoc khi doi DNS, installer luu DNS hien tai vao `C:\ProgramData\AdultContentShutdownGuard\dns-backup.json`. File cau hinh, danh sach cache va secret go cai dat duoc khoa quyen ghi cho tai khoan thuong.

Kiem tra:

```text
http://127.0.0.1:8765/health
```

## Cap nhat

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-service.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\apply-installed-service-update.ps1
```

## Go cai dat

Ma go cai dat thay doi theo gio va chi co Quan tri vien lay duoc:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\show-uninstall-code.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-service.ps1 -UninstallCode <MA_HIEN_TAI>
```

Uninstaller xoa firewall rule cua dich vu va khoi phuc DNS da sao luu truoc khi cai dat.

## Cau hinh

File: `src\Guard.Service\appsettings.json`

- `DryRun`: dat `true` de chi ghi log khi thu nghiem.
- `Dns.ListenAddresses`: phai giu ca `127.0.0.1` va `::1` de khong co duong vuot qua IPv6.
- `BlocklistUpdates.RemoteUrl` va `Sha256`: them danh sach chan ngoai neu co, bat buoc la HTTPS va SHA-256 dung.
- `ProcessRules.BlockedProcessNames`: bo sung ten tien trinh vuot qua can chan.

## Kiem thu

```powershell
dotnet test .\AdultContentShutdownGuard.sln -c Release
```

Kiem tra them tren may cai dat bang PowerShell Quan tri vien:

```powershell
Get-DnsClientServerAddress
Get-NetFirewallRule -DisplayName 'AdultContentShutdownGuard*'
Get-Service AdultContentShutdownGuard
```
