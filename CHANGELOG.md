# Değişiklik Günlüğü / Changelog

[Türkçe](#türkçe) · [English](#english)

---

## Türkçe

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) esas alınmıştır.

### [1.1.0] - 2026-11-20

#### Eklendi
- **Saturation (Windows Renk Filtresi, %0–%400):** Resmi `System32\Magnification.dll` (`MagSetFullscreenColorEffect`) üzerinden çalışan 5×5 renk matrisi katmanı. GPU Vibrance sınırının ötesine çıkar; borderless/windowed oyunlarda ve masaüstünde çalışır, anti-cheat riski taşımaz. `%100` iken tamamen kapalıdır (0 FPS kaybı, belleğe yüklenmez).
- **Overdrive** hazır ön ayarı (yüksek GPU Vibrance + %220 Renk Filtresi).
- **Sürüklenebilir Before / After Canlı Önizleme:** Önizleme sekmesine gömülü oyun sahnesiyle ortadan kaydırılabilir karşılaştırma ekranı, hızlı test butonları (**Mevcut Ayar · Rekabetçi FPS · Overdrive**) ve **Fotoğraf Seç** (kendi görselini yükleme) desteği.
- **Genel Sekme Canlı Showcase:** Slider'ları oynatırken anlık farkı gösteren gömülü Before / After kartı ve monitöre uygulanan doygunluğu dengeleyen ters ekran telafisi.

#### Değişti
- AMD ADL doygunluk üst sınırı tekrar `400` olarak geri yüklendi (`Math.Max(400, max)`).
- Yapılandırma şeması `v3` sürümüne yükseltildi (`DesktopFilterSaturation`, `TargetFilterSaturation`, preset ve zamanlayıcı filtre alanları için otomatik göç).

#### Düzeltildi
- Önizleme ekranında monitör doygunluğundan kaynaklanan çift doygunluk (double-saturation) ve yüksek değerlerde pikselleşme/banding sorunu giderildi.
- Önizleme sekmesi masaüstü ayarları normalken iki tarafın aynı görünmesi düzeltildi.

### [1.0.0] - 2026-11-18

#### Eklendi
- Discord düzeni + Spotify cilası ile tamamen yeni arayüz (ikon rayı, profil paneli, durum çubuğu).
- Hazır ön ayarlar (Rekabetçi FPS, Sinematik, Doğal, Göz Rahatlığı, Okuma) ve özel ön ayar kaydı.
- Profil kopyalama, yeniden adlandırma, JSON içe/dışa aktarma.
- Profil başına uygulama / geri dönüş gecikmesi, "sadece tam ekranda uygula", tam yol eşleştirme.
- Çoklu monitör: monitör başına ayrı değerler.
- Zamanlayıcı (gece modu).
- Geri al / yinele, Önce/Sonra karşılaştırma, genişletilmiş canlı önizleme (gri tonlama + ten rengi).
- İlk açılış sihirbazı ve GPU testi, Hakkında sayfası, OLED siyah tema, yerel kullanım istatistiği.
- Döngüsel yerel hata günlüğü ve "Günlükleri Aç" butonu.
- PerMonitorV2 DPI desteği (`app.manifest`).

#### Değişti
- Pencere takibi tamamen olay tabanlı hale getirildi (1,5 sn'lik polling kaldırıldı).
- Yapılandırma kaydı atomik (`File.Replace`) + `.bak` yedeği + sürüm göçü.
- Tek örnek: ikinci açılışta mevcut pencere öne getirilir.
- Slider sürüklemede disk kaydı debounce, sürücü çağrıları throttle edildi (kasma giderildi).

#### Düzeltildi
- Uyku / kilit / çözünürlük değişimi sonrası renkler yeniden uygulanıyor.
- Program çökerse veya düzgün kapanmazsa renkler sıfırlanıyor.
- Tam ekranda görev çubuğunun kapanması ve Aero Snap sorunları.
- Türkçe karakterlerin derlemede bozulması (`/codepage:65001`).
- NVIDIA'da desteklenmeyen ayarlar artık açıkça belirtiliyor.

---

## English

Format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

### [1.1.0] - 2026-11-20

#### Added
- **Saturation (Windows Color Filter, 0%–400%):** Native 5×5 color matrix layer using `System32\Magnification.dll` (`MagSetFullscreenColorEffect`). Goes past GPU hardware limits in borderless/windowed games and desktop with zero anti-cheat hooks. Completely inactive and unloaded at 100% (0 FPS overhead).
- **Overdrive** built-in preset (high GPU Vibrance + 220% Color Filter).
- **Draggable Before / After Live Preview:** Embedded game scene with a split comparison slider in the Preview tab, quick comparison modes (**Current · Competitive FPS · Overdrive**), and custom image loading (**Load Image**).
- **General Tab Live Showcase:** Real-time Before / After card directly above the desktop sliders with inverse screen saturation compensation.

#### Changed
- Restored AMD ADL saturation upper limit to `400` (`Math.Max(400, max)`).
- Upgraded config schema to `v3` with automatic migration for filter saturation fields.

#### Fixed
- Eliminated double-saturation and color banding/pixelation in live preview when high display saturation is active.
- Fixed Preview tab showing identical sides when desktop settings are at neutral 100%.

### [1.0.0] - 2026-11-18

#### Added
- Brand-new UI with Discord layout + Spotify polish (icon rail, profile sidebar, status bar).
- Built-in presets (Competitive FPS, Cinematic, Natural, Eye Care, Reading) and custom preset saving.
- Profile duplication, renaming, and JSON import/export.
- Per-profile apply/revert delays, "apply only in fullscreen", and full-path matching.
- Multi-monitor support with per-monitor color values.
- Night-mode schedule rules.
- Undo/redo, Before/After comparison, and expanded live preview (grayscale + skin tones).
- First-run onboarding wizard with GPU test, About page, OLED black theme, and local usage stats.
- Rotating local error log and "Open Logs" button.
- PerMonitorV2 DPI support (`app.manifest`).

#### Changed
- Window tracking switched to fully event-driven (removed 1.5s polling).
- Atomic configuration saving (`File.Replace`) with `.bak` backup and schema migration.
- Single-instance enforcement: second launch brings the existing window to the foreground.
- Debounced disk writes and throttled driver calls during slider dragging (eliminates stutter).

#### Fixed
- Re-applies colors after sleep, lock screen, or display resolution changes.
- Resets colors automatically on crash or dirty shutdown.
- Taskbar overlap in maximized mode and Aero Snap issues.
- Corrupted Turkish characters during compilation (`/codepage:65001`).
- Unsupported settings on NVIDIA are now clearly marked in the UI.
