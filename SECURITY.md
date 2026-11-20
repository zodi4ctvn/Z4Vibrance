# Güvenlik Politikası / Security Policy

[Türkçe](#türkçe) · [English](#english)

---

## Türkçe

### Güvenlik Açığı Bildirimi

Bir güvenlik açığı veya sürücü/sistem uyumluluğuyla ilgili kritik bir sorun tespit ederseniz, lütfen herkese açık Issue açmak yerine GitHub üzerinden **Private Vulnerability Reporting** (Security → Advisories) özelliğini kullanın veya geliştiriciyle iletişime geçin.

### Mimari Güvenlik İlkeleri

- **Tamamen Yerel**: Z4Vibrance hiçbir ağ bağlantısı kurmaz, telemetri toplamaz ve otomatik güncelleme yapmaz.
- **Sıfır Üçüncü Parti Bağımlılık**: Yalnızca .NET Framework 4.x BCL, resmi GPU API'leri (`atiadlxx.dll`, `nvapi64.dll`) ve standart Win32/GDI API'leri kullanılır.
- **Anti-Cheat Uyumu**: Oyun süreçlerine müdahale (DLL injection, bellek okuma/yazma, overlay, klavye hook'u, `RegisterHotKey`) kesinlikle içermez.

---

## English

### Reporting a Vulnerability

If you discover a security vulnerability or a critical driver/system compatibility issue, please do **not** open a public Issue. Instead, use GitHub's **Private Vulnerability Reporting** (Security → Advisories) or contact the maintainer directly.

### Architectural Security Principles

- **Fully Local**: Z4Vibrance makes no network connections, collects no telemetry, and performs no automatic updates.
- **Zero Third-Party Dependencies**: Uses only the .NET Framework 4.x BCL, official GPU vendor APIs (`atiadlxx.dll`, `nvapi64.dll`), and standard Win32/GDI APIs.
- **Anti-Cheat Compliance**: Contains zero interference with game processes (no DLL injection, memory read/write, overlays, keyboard hooks, or `RegisterHotKey`).
