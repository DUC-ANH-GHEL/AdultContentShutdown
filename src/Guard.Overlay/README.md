# Màn hình tạm dừng

Ứng dụng này phủ toàn màn hình trên mọi màn hình đang dùng và tự đóng khi hết thời gian.
Nó giữ trên cùng các ứng dụng thông thường, kể cả khi người dùng chuyển sang ứng dụng khác.

Không can thiệp vào màn hình bảo mật của Windows như UAC, màn hình khóa hoặc `Ctrl + Alt + Del`.
Ứng dụng cũng không cài hook bàn phím và không tắt Trình quản lý tác vụ.

## Chạy thử 10 giây

```powershell
dotnet run --project .\src\Guard.Overlay\Guard.Overlay.csproj -- --duration-seconds 10
```

Không truyền tham số thì thời lượng mặc định là 5 phút. Thời lượng hợp lệ từ 5 giây đến 60 phút.
