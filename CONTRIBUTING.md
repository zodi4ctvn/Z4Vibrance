# Katkıda Bulunma / Contributing

[Türkçe](#türkçe) · [English](#english)

---

## Türkçe

Z4Vibrance'e katkıda bulunmak istediğiniz için teşekkürler! Pull Request göndermeden önce aşağıdaki kurallara uyduğunuzdan emin olun:

1. **Üçüncü Parti Bağımlılık Yok**: NuGet paketi, harici `.exe` veya harici `.dll` eklemeyin. Proje yalnızca .NET Framework 4.x (`csc.exe`) ve standart Windows API'leri ile derlenmelidir.
2. **Anti-Cheat ve Sistem Güvenliği**:
   - `RegisterHotKey`, `WH_KEYBOARD` / `WH_MOUSE` hook'ları, overlay, DLL injection veya oyun süreçlerinin belleğine erişim **kesinlikle kabul edilmez**.
   - Ağ bağlantısı, telemetri veya uzaktan kod indirme eklemeyin.
3. **Yerelleştirme**: Arayüze eklenen her yeni metin [Core/Localization.cs](Core/Localization.cs) üzerinden hem Türkçe (`tr`) hem İngilizce (`en`) olarak eklenmelidir.
4. **Tema Tutarlılığı**: Renk, boşluk, yazı tipi ve köşe yarıçapı sabitleri yalnızca [UI/Theme.cs](UI/Theme.cs) içinde tanımlanmalıdır.
5. **Derleme Doğrulaması**: Değişiklik sonrası `powershell -ExecutionPolicy Bypass -File .\build.ps1` komutunun hatasız çalıştığını doğrulayın.

---

## English

Thank you for your interest in contributing to Z4Vibrance! Before submitting a Pull Request, please make sure you follow these guidelines:

1. **Zero Third-Party Dependencies**: Do not add NuGet packages, external `.exe`, or external `.dll` files. The project must compile using only .NET Framework 4.x (`csc.exe`) and standard Windows APIs.
2. **Anti-Cheat & System Safety**:
   - `RegisterHotKey`, `WH_KEYBOARD` / `WH_MOUSE` hooks, overlays, DLL injection, or reading/writing game process memory are **strictly rejected**.
   - Do not add network connections, telemetry, or remote code downloads.
3. **Localization**: Every new UI string must be added in both Turkish (`tr`) and English (`en`) via [Core/Localization.cs](Core/Localization.cs).
4. **Theme Consistency**: All color, spacing, font, and corner-radius tokens must be defined exclusively in [UI/Theme.cs](UI/Theme.cs).
5. **Build Verification**: Verify that `powershell -ExecutionPolicy Bypass -File .\build.ps1` compiles cleanly with zero errors before opening a PR.
