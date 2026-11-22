<p align="center">
  <img src="assets/icon.png" width="120" alt="Claude Launcher">
</p>

<h1 align="center">Claude Launcher</h1>

<p align="center">
  Jalankan beberapa akun <b>Claude Desktop</b> sekaligus, masing-masing dengan login dan data sendiri,<br>
  lalu susun semua jendelanya di layar dengan satu klik.<br><br>
  <b>Windows 10/11</b> (x64 &amp; ARM64) · <b>macOS 14+</b> (Apple Silicon &amp; Intel)
</p>

---

## Daftar isi

- [Apa ini?](#apa-ini)
- [Fitur](#fitur)
- [Cara kerjanya](#cara-kerjanya)
- [Instalasi Windows](#instalasi-windows)
  - [Opsi A: Pakai file jadi (zip)](#opsi-a-pakai-file-jadi-zip)
  - [Opsi B: Build sendiri dari source](#opsi-b-build-sendiri-dari-source)
  - [Uninstall](#uninstall)
- [Instalasi macOS](#instalasi-macos)
- [Cara pakai](#cara-pakai)
  - [1. Pertama kali dibuka](#1-pertama-kali-dibuka)
  - [2. Menambah instance (akun baru)](#2-menambah-instance-akun-baru)
  - [3. Membuka & login](#3-membuka--login)
  - [4. Menu kartu instance](#4-menu-kartu-instance)
  - [5. Buka & rapikan jendela](#5-buka--rapikan-jendela)
  - [6. Editor tata letak & layout custom](#6-editor-tata-letak--layout-custom)
  - [7. Preset](#7-preset)
  - [8. Template & salin setup](#8-template--salin-setup)
  - [9. Info limit pemakaian](#9-info-limit-pemakaian)
  - [10. System tray, autostart, hotkey](#10-system-tray-autostart-hotkey)
- [Hotkey](#hotkey)
- [Lokasi data](#lokasi-data)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Struktur project](#struktur-project)
- [Catatan privasi](#catatan-privasi)

---

## Apa ini?

Claude Desktop secara bawaan hanya bisa login ke **satu akun** dalam satu waktu. Kalau kamu punya beberapa akun (pribadi, kantor, klien, akun cadangan saat limit habis), kamu harus logout dan login bolak-balik.

**Claude Launcher** menyelesaikan itu. Setiap "instance" adalah Claude Desktop yang dijalankan dengan folder data terpisah, jadi:

- login, cookie, riwayat chat, dan setting tiap akun tidak saling tercampur;
- semua akun bisa terbuka **bersamaan**, masing-masing di jendelanya sendiri;
- launcher menampilkan status tiap instance (berjalan atau tidak, email yang login, sisa limit terakhir), dan bisa menyusun semua jendela ke grid/kolom/zona custom di monitor yang kamu pilih.

Launcher ini **tidak** memodifikasi Claude Desktop, tidak menyentuh token login, dan tidak butuh hak admin.

## Fitur

| Fitur | Keterangan |
|---|---|
| Multi-instance | Tambah instance sebanyak yang kamu mau, masing-masing login ke akun berbeda. |
| Status live | Titik hijau dan label "Berjalan" untuk instance yang sedang jalan, diperbarui tiap 2,5 detik. |
| Email akun | Menampilkan email yang sedang login di tiap instance. |
| Info limit | Bar pemakaian "5 jam" dan "Mingguan" dari snapshot terakhir yang di-cache Claude. |
| Buka & rapikan | Membuka semua instance yang aktif lalu menyusun jendelanya otomatis. |
| 5 template layout | Focus, Kolom, Baris, Grid, Priority Grid. |
| Layout custom | Editor zona drag-and-drop: geser, ubah ukuran, bagi dua, snap ke tepi. |
| Multi-monitor | Pilih monitor tujuan; DPI tiap monitor diperhitungkan. |
| Preset | Simpan kombinasi instance + layout + monitor, jalankan lagi dengan satu klik. |
| Template & salin setup | Salin MCP server, preferensi, toggle tool, preset sesi, dan extension antar instance (login tidak ikut). |
| System tray | Tetap berjalan di tray saat jendela ditutup; menu cepat untuk buka instance. |
| Autostart | Jalan otomatis saat login Windows, opsional langsung "Buka & rapikan". |
| Hotkey global | `Ctrl+Alt+1…9` buka instance ke-N, `Ctrl+Alt+0` buka & rapikan. |
| Warna & urutan | Beri warna avatar, ubah urutan kartu, ganti nama. |

## Cara kerjanya

Claude Desktop dibangun dengan Electron. Electron mendukung argumen `--user-data-dir=<folder>`: proses yang dijalankan dengan folder data berbeda dianggap aplikasi terpisah, punya sesi login sendiri, dan bisa jalan berdampingan dengan yang lain.

Claude Launcher memakai mekanisme itu:

```
Instance "Utama"   → Claude Desktop biasa (folder data bawaan)
Instance "Kantor"  → claude-desktop.exe --user-data-dir="%USERPROFILE%\.claude-launcher\profiles\kantor"
Instance "Klien A" → claude-desktop.exe --user-data-dir="%USERPROFILE%\.claude-launcher\profiles\klien-a"
```

- **Deteksi instance yang berjalan:** launcher membaca command line proses `claude.exe` milik Claude Desktop (bukan Claude Code CLI yang namanya sama) dan mencocokkan `--user-data-dir`-nya dengan profil.
- **Menyusun jendela:** di Windows memakai Win32 (`EnumWindows`, `SetWindowPos`, info monitor dan DPI); di macOS memakai Accessibility API.
- **Email & limit:** dibaca dari cache IndexedDB claude.ai milik masing-masing instance. Hanya UUID akun, record akun, dan snapshot rate-limit yang dibaca; token tidak pernah disentuh.

## Instalasi Windows

### Kebutuhan

- Windows 10 (versi 1903 atau lebih baru) atau Windows 11, x64 atau ARM64.
- **Claude Desktop** sudah terinstall dari <https://claude.ai/download>. Versi Microsoft Store/MSIX maupun installer lama (Squirrel) sama-sama didukung.
- Tidak perlu install .NET. Exe hasil build sudah self-contained.

### Opsi A: Pakai file jadi (zip)

1. Ambil `ClaudeLauncher-win-x64.zip` (atau `ClaudeLauncher-win-arm64.zip` untuk PC ARM seperti Snapdragon) dari halaman [**Releases**](https://github.com/harisisd/claude-launcher/releases/latest), atau dari folder `win\build\` kalau kamu build sendiri.
2. Extract zip-nya. Isinya `ClaudeLauncher.exe` dan `install.ps1`.
3. Pilih salah satu:
   - **Portable:** klik dua kali `ClaudeLauncher.exe`. Selesai.
   - **Install** (Start Menu + shortcut Desktop, tanpa admin): klik kanan di folder hasil extract, pilih *Open in Terminal*, lalu jalankan:

     ```powershell
     powershell -ExecutionPolicy Bypass -File .\install.ps1
     ```

     Program disalin ke `%LOCALAPPDATA%\Programs\Claude Launcher\`. Tambahkan `-NoDesktopShortcut` kalau tidak mau shortcut di Desktop.

> **Windows SmartScreen** bisa menampilkan "Windows protected your PC" karena exe belum ditandatangani (unsigned). Klik **More info → Run anyway**. Ini normal untuk aplikasi open source yang di-build sendiri.

### Opsi B: Build sendiri dari source

1. Install **.NET 10 SDK**: <https://dotnet.microsoft.com/download/dotnet/10.0>

   ```powershell
   winget install Microsoft.DotNet.SDK.10
   ```

2. Clone repo ini:

   ```powershell
   git clone https://github.com/harisisd/claude-launcher.git
   ```

   Kalau clone gagal dengan error `Filename too long`, aktifkan long path di git lalu clone ulang:

   ```powershell
   git config --global core.longpaths true
   ```

3. Build:

   ```powershell
   cd claude-launcher\win
   ```

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build.ps1
   ```

   Hasilnya:

   ```
   win\build\win-x64\ClaudeLauncher.exe        (~66 MB, self-contained, single file)
   win\build\win-arm64\ClaudeLauncher.exe      (~62 MB)
   win\build\ClaudeLauncher-win-x64.zip
   win\build\ClaudeLauncher-win-arm64.zip
   ```

   Opsi `build.ps1`:

   | Opsi | Fungsi |
   |---|---|
   | `-Arch x64` / `-Arch arm64` / `-Arch all` | Pilih arsitektur (default `all`). |
   | `-Install` | Setelah build, langsung install versi yang cocok untuk PC ini. |

   Contoh build dan install sekaligus:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Arch x64 -Install
   ```

   Untuk development cepat (butuh .NET 10 runtime terpasang):

   ```powershell
   dotnet run --project win\ClaudeLauncher.csproj
   ```

### Uninstall

`install.ps1` ikut disalin ke folder program, jadi uninstall bisa dari mana saja:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\Claude Launcher\install.ps1" -Uninstall
```

Uninstall saat masih memakai versi portable (tanpa install) cukup dengan menghapus `ClaudeLauncher.exe`, lalu matikan *Jalan otomatis saat login* sebelumnya kalau pernah dinyalakan.

Perintah ini menghapus program, shortcut, dan entri autostart. **Profil dan data login di `%USERPROFILE%\.claude-launcher` tidak dihapus.** Hapus folder itu manual kalau memang ingin membersihkan semuanya (semua login instance tambahan akan hilang).

## Instalasi macOS

Butuh macOS 14 Sonoma atau lebih baru dan Xcode (bukan hanya Command Line Tools, karena macro SwiftUI ada di Xcode).

```bash
cd mac
./build.sh          # build universal (arm64 + x86_64) ke ~/Applications/Claude Launcher.app
./make-dmg.sh       # opsional: buat mac/build/ClaudeLauncher.dmg
```

`build.sh` memakai `DEVELOPER_DIR=/Applications/Xcode-beta.app/...` secara default. Kalau Xcode kamu ada di tempat lain:

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer ./build.sh
```

Untuk fitur "Buka & rapikan", beri izin di **System Settings → Privacy & Security → Accessibility → Claude Launcher**. Hotkey di macOS memakai `⌃⌥1…9` dan `⌃⌥0`.

## Cara pakai

### 1. Pertama kali dibuka

Saat pertama dibuka, launcher membuat satu kartu bernama **Utama**. Kartu ini adalah Claude Desktop kamu yang biasa (folder data bawaan), jadi akun yang sudah login di sana langsung terlihat.

Bagian atas jendela menampilkan ringkasan: `N instance · N berjalan · N aktif`.

- **berjalan** = jumlah instance yang sedang terbuka.
- **aktif** = jumlah instance yang ikut "Buka & rapikan" (switch di kartu menyala).

### 2. Menambah instance (akun baru)

1. Klik **+ Tambah**.
2. Ketik nama, misalnya `Kantor`. Di bawah kolom nama terlihat folder data yang akan dipakai (`~\.claude-launcher\profiles\kantor`).
3. Klik **Tambah**. Kartu baru muncul. Folder datanya baru dibuat saat instance pertama kali dibuka.

Kalau ada instance yang ditandai **Template**, setup-nya (MCP, preferensi, dll.) otomatis disalin ke instance baru. Lihat [Template & salin setup](#8-template--salin-setup).

### 3. Membuka & login

- Klik **Buka** (atau klik dua kali kartunya). Jendela Claude Desktop baru terbuka.
- Instance baru masih kosong, jadi **login dengan akun yang berbeda** di jendela itu.
- Setelah login, kartu menampilkan email akun tersebut.
- Kalau instance sudah berjalan, tombolnya berubah jadi **Tampilkan** untuk membawa jendelanya ke depan.

### 4. Menu kartu instance

Klik **⋯** di kartu atau klik kanan kartu:

| Menu | Fungsi |
|---|---|
| Buka / Tampilkan | Membuka instance atau membawa jendelanya ke depan. |
| Tutup instance | Menutup Claude di instance itu (dengan konfirmasi). |
| Ganti nama… | Mengubah nama tampilan. Folder data dan login tidak berubah. |
| Salin setup dari… | Menyalin setup dari instance lain ke instance ini. |
| Jadikan template / Berhenti jadi template | Menandai instance sebagai sumber setup untuk instance baru. |
| Warna | Warna avatar (8 pilihan). |
| Pindah ke atas / bawah | Mengubah urutan. Urutan menentukan nomor hotkey dan nomor zona. |
| Buka folder data | Membuka folder data instance di File Explorer. |
| Hapus dari daftar | Menghapus kartu dari launcher. **Folder data dan login tidak dihapus**, jadi kalau kamu menambah instance dengan nama yang sama, login lamanya kembali. |

### 5. Buka & rapikan jendela

1. Nyalakan **switch** di kartu yang ingin ikut disusun.
2. Klik **Buka & rapikan (N)**.

Launcher akan:

1. membuka instance aktif yang belum berjalan;
2. menunggu sampai semua jendela muncul (maksimal 30 detik; statusnya terlihat di tombol, misalnya `Menunggu 2/3`);
3. menyusun jendela ke layout dan monitor yang dipilih. Jendela yang di-minimize atau di-maximize dikembalikan dulu ke ukuran normal.

Jendela ke-1 masuk zona 1, jendela ke-2 masuk zona 2, dan seterusnya mengikuti urutan kartu. Kalau jumlah jendela lebih banyak dari zona, sisanya diputar ulang dari zona pertama.

### 6. Editor tata letak & layout custom

Klik ikon **⚙** di kanan atas.

- **Monitor** (baris paling atas): pilih monitor tujuan. Setiap kartu menampilkan nomor, resolusi, dan nama monitor.
- **Instance**: centang instance yang ikut disusun (sama dengan switch di kartu).
- **Template**: pilih salah satu layout bawaan. Pratinjaunya menyesuaikan jumlah instance yang dicentang.
  - *Focus*: jendela bertumpuk sedikit bergeser di tengah layar.
  - *Kolom*: dibagi rata ke samping.
  - *Baris*: dibagi rata ke bawah.
  - *Grid*: sampai 3 jendela berdampingan, lebih dari itu jadi grid kotak (baris terakhir melebar).
  - *Priority Grid*: satu jendela besar di tengah, sisanya di kiri dan kanan (minimal 3 jendela).
- **Custom**: layout buatanmu sendiri (bisa diedit dan dihapus).
- **Beri jarak antar jendela**: celah 8 px (disesuaikan dengan DPI monitor) di antara jendela.

Klik **Simpan** untuk menyimpan pilihan.

**Membuat layout custom:** klik **+ Buat layout baru**. Editor zona terbuka dengan kanvas seukuran area kerja monitor yang dipilih:

| Aksi | Cara |
|---|---|
| Pindahkan zona | Drag bagian tengah zona. |
| Ubah ukuran | Drag tepi atau sudut zona. |
| Bagi kiri/kanan | Klik dua kali zona. |
| Bagi atas/bawah | `Shift` + klik dua kali. |
| Tambah zona | Tombol **+ Zona**. |
| Hapus zona | Pilih zona lalu tekan `Delete`, klik ✕ di pojok zona, atau tombol **Hapus zona**. |
| Reset cepat | Tombol **Kolom** atau **Grid**. |

Zona otomatis menempel (snap) ke tepi layar dan tepi zona lain, dan ukurannya dibulatkan ke 1%. Tiap zona menampilkan ukuran piksel dan persentasenya. Beri nama layout, lalu klik **Simpan**.

### 7. Preset

Preset menyimpan kombinasi: instance aktif + layout + monitor + jarak.

- **Simpan:** menu **≡** → *Simpan tata letak sekarang sebagai preset…* → beri nama. Nama yang sama menimpa preset lama.
- **Jalankan:** menu **≡** → klik nama preset, atau dari menu tray → *Preset*. Launcher langsung menerapkan setting preset lalu menjalankan "Buka & rapikan".
- **Hapus:** menu **≡** → *Hapus preset*.

Contoh: preset "Kerja" (3 akun kantor, Grid, monitor 1) dan preset "Riset" (2 akun, Kolom, monitor 2).

### 8. Template & salin setup

Supaya tidak perlu mengatur ulang MCP server dan preferensi di tiap akun:

1. Atur satu instance dengan lengkap, misalnya **Utama**.
2. Kartu → **⋯** → **Jadikan template**. Kartu itu diberi label *Template*.
3. Setiap instance baru otomatis mendapat salinan setup dari template.

Untuk instance yang sudah ada: kartu tujuan → **⋯** → **Salin setup dari…**, pilih sumber, lalu centang yang ingin disalin:

| Opsi | Isi |
|---|---|
| MCP server (mode chat) | `mcpServers` di `claude_desktop_config.json` |
| Preferensi aplikasi | Setting lain di `claude_desktop_config.json` (Cowork, browser, izin, dll.), kecuali state sidebar/sesi yang khusus per mesin |
| Status on/off tool MCP | `mcp-user-tool-toggles.json` |
| Preset sesi Claude Code | `ccd-session-presets.json` |
| Extension | Folder `Claude Extensions` dan `Claude Extensions Settings` |

Catatan:

- **Login tidak pernah disalin.**
- File lama di tujuan dibackup dulu ke `<folder data tujuan>\launcher-backup\<tanggal-jam>\`.
- Instance tujuan harus **ditutup** dulu, karena Claude menimpa setting-nya sendiri saat keluar.
- Skill, memory, `CLAUDE.md`, dan MCP milik Claude Code (`%USERPROFILE%\.claude`) sudah otomatis sama di semua instance, jadi tidak perlu disalin.

### 9. Info limit pemakaian

Baris kecil di tiap kartu:

```
5 jam ▬▬───── 24%   Mingguan ▬▬▬▬── 56%   · data 22 Nov
```

- Bar berubah merah saat pemakaian ≥ 90%. Tulisan **reset** berarti waktu reset-nya sudah lewat.
- Arahkan mouse ke baris itu untuk melihat jam reset dan waktu snapshot.
- **Ini bukan angka live.** Angkanya diambil dari snapshot rate-limit terakhir yang tersimpan di cache Claude bersama percakapan, jadi bisa tertinggal beberapa jam atau hari. Untuk angka terkini, buka popup limit di dalam Claude.

### 10. System tray, autostart, hotkey

Semua ada di menu **≡**:

| Pengaturan | Fungsi |
|---|---|
| Jalan otomatis saat login | Menambahkan launcher ke startup Windows (`HKCU\...\Run`). Saat jalan otomatis, launcher langsung masuk ke tray tanpa membuka jendela. |
| Buka & rapikan otomatis saat launcher jalan otomatis | 4 detik setelah login, semua instance aktif dibuka dan disusun. |
| Tutup jendela = tetap jalan di system tray | Menutup jendela tidak menghentikan launcher (default: nyala). Matikan kalau ingin launcher keluar saat jendelanya ditutup. |
| Hotkey global | Mengaktifkan atau mematikan hotkey `Ctrl+Alt+…`. |
| Daftar hotkey… | Menampilkan hotkey untuk tiap instance. |
| Keluar dari launcher | Menghentikan launcher sepenuhnya. Instance Claude yang terbuka tetap berjalan. |

**Ikon tray:** klik kiri untuk membuka jendela launcher. Klik kanan untuk menu cepat: daftar instance (● berjalan, ○ tidak), *Buka & rapikan*, *Preset*, *Tutup semua instance*, dan *Keluar*.

Launcher hanya berjalan satu kali. Membuka exe-nya lagi akan memunculkan jendela yang sudah ada.

## Hotkey

| Windows | macOS | Aksi |
|---|---|---|
| `Ctrl+Alt+1` … `Ctrl+Alt+9` | `⌃⌥1` … `⌃⌥9` | Buka atau tampilkan instance ke-1 sampai ke-9 (urutan kartu) |
| `Ctrl+Alt+0` | `⌃⌥0` | Buka & rapikan |

Kalau ada hotkey yang sudah dipakai aplikasi lain, hotkey itu dilewati. *Daftar hotkey…* menampilkan mana saja yang gagal didaftarkan. Di keyboard dengan tombol AltGr, `Ctrl+Alt` sama dengan AltGr, jadi matikan hotkey global kalau bentrok dengan pengetikan karakter khusus.

## Lokasi data

| Apa | Windows | macOS |
|---|---|---|
| Setting launcher | `%USERPROFILE%\.claude-launcher\` | `~/.claude-launcher/` |
| Folder data instance tambahan | `%USERPROFILE%\.claude-launcher\profiles\<id>\` | `~/.claude-launcher/profiles/<id>/` |
| Folder data instance "Utama" | Dideteksi otomatis: `%LOCALAPPDATA%\Claude-Data\claude_*` (MSIX terbaru), `%APPDATA%\Claude` (installer lama), atau `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude` | `~/Library/Application Support/Claude` |
| Program (setelah install.ps1) | `%LOCALAPPDATA%\Programs\Claude Launcher\` | `~/Applications/Claude Launcher.app` |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClaudeLauncher` | `~/Library/LaunchAgents/com.hariz.claudelauncher.plist` |

File di `.claude-launcher\`:

| File | Isi |
|---|---|
| `profiles.json` | Daftar instance: id, nama, warna, template |
| `layout.json` | Monitor, layout, jarak, dan instance aktif yang terakhir disimpan |
| `layouts.json` | Layout custom |
| `presets.json` | Preset |
| `settings.json` | Autostart, tray, hotkey |

Format JSON-nya sama di Windows dan macOS. Folder `profiles\<id>` berisi **data login** akun tersebut, jadi jangan dibagikan ke orang lain.

## Troubleshooting

**"Claude Desktop tidak ditemukan"**
Install Claude Desktop dari <https://claude.ai/download>. Launcher mencari, berurutan:
`%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe` (alias aplikasi MSIX), lalu `%LOCALAPPDATA%\AnthropicClaude\claude.exe` (installer lama), lalu folder paket MSIX dari registry. Kalau versi MSIX sudah terinstall tapi tetap tidak ditemukan, cek **Settings → Apps → Advanced app settings → App execution aliases** dan pastikan alias Claude menyala.

**Instance baru terbuka tapi langsung menampilkan akun Utama**
Pastikan kamu membuka lewat launcher, bukan dari Start Menu. Cek juga bahwa folder `%USERPROFILE%\.claude-launcher\profiles\<id>` terisi file setelah instance dibuka.

**Status "Berjalan" tidak muncul**
Status diperbarui tiap 2,5 detik. Launcher hanya mengenali proses Claude Desktop yang dijalankan dari paket MSIX (`WindowsApps\Claude_*`) atau folder `AnthropicClaude`. Instance yang dijalankan dengan cara lain tidak terdeteksi.

**"Sebagian belum muncul" saat Buka & rapikan**
Ada jendela yang belum muncul dalam 30 detik (misalnya Claude sedang update, atau jendelanya tersembunyi di tray). Tunggu sampai semua terbuka, lalu klik **Buka & rapikan** lagi.

**Ukuran jendela meleset sedikit**
Windows menambahkan border transparan di sekitar jendela, dan launcher mengkompensasinya lewat DWM. Kalau masih meleset setelah pindah monitor dengan DPI berbeda, klik **Buka & rapikan** sekali lagi.

**Email atau limit tidak tampil**
Email baru muncul setelah login dan Claude sempat menyimpan cache akun. Limit baru muncul setelah ada percakapan yang menyimpan snapshot rate-limit. "Limit: belum ada data" artinya cache-nya memang belum ada.

**Hotkey tidak bekerja**
Pastikan *Hotkey global* menyala di menu ≡, lalu cek *Daftar hotkey…* untuk kombinasi yang dipakai aplikasi lain.

**SmartScreen / antivirus memblokir exe**
Exe belum ditandatangani. Pilih *More info → Run anyway*, atau build sendiri dari source.

**`git clone` error "Filename too long"**
Jalankan `git config --global core.longpaths true`, lalu clone ulang ke folder dengan path pendek (misalnya `C:\src`).

## FAQ

**Apakah ini melanggar aturan atau mengubah Claude?**
Launcher hanya menjalankan Claude Desktop resmi dengan argumen `--user-data-dir`, yaitu fitur standar Electron. Tidak ada file Claude yang diubah. Tetap patuhi ketentuan layanan Anthropic untuk akun-akun yang kamu pakai.

**Berapa banyak instance yang bisa dijalankan?**
Tidak ada batas dari launcher. Setiap instance adalah proses Claude Desktop lengkap (sekitar 300–600 MB RAM), jadi batasnya ada di RAM PC kamu. Hotkey hanya tersedia untuk 9 instance pertama.

**Kalau kartu dihapus, apakah login-nya hilang?**
Tidak. "Hapus dari daftar" hanya menghapus kartu. Folder datanya tetap ada, dan kalau kamu menambah instance dengan nama yang sama (id yang sama), login-nya kembali.

**Apakah Claude Code (`~/.claude`) ikut terpisah?**
Tidak. Konfigurasi Claude Code (skill, memory, `CLAUDE.md`, MCP) memakai `%USERPROFILE%\.claude` yang sama untuk semua instance. Yang terpisah adalah data aplikasi Claude Desktop: login, chat, preferensi, dan MCP mode chat.

**Bisa pindahkan profil dari Mac ke Windows?**
File setting launcher (`profiles.json`, `layouts.json`, `presets.json`) kompatibel. Folder data login sebaiknya tidak dipindah; login ulang saja di mesin baru.

## Struktur project

```
claude-launcher/
├── assets/
│   └── icon.png               Ikon aplikasi (1024×1024), dipakai kedua platform
├── mac/                       Versi macOS (SwiftUI, satu file)
│   ├── ClaudeLauncher.swift
│   ├── Info.plist
│   ├── build.sh               Build universal .app ke ~/Applications
│   └── make-dmg.sh            Bungkus .app jadi .dmg
└── win/                       Versi Windows (C# / WPF, .NET 10)
    ├── ClaudeLauncher.csproj
    ├── app.manifest           Per-monitor DPI v2, tanpa admin
    ├── AppIcon.ico            Dibuat dari assets/icon.png oleh build.ps1
    ├── App.xaml(.cs)          Tema, single-instance, system tray, hotkey global
    ├── MainWindow.cs          Daftar kartu instance
    ├── LayoutEditor.cs        Pemilih monitor, template, layout custom
    ├── ZoneEditor.cs          Kanvas editor zona
    ├── Dialogs.cs             Dialog nama, konfirmasi, salin setup
    ├── Ui.cs                  Komponen UI (tombol pill, switch, kartu)
    ├── AppModel.cs            State, penyimpanan JSON, buka & rapikan, preset, autostart
    ├── ClaudeProcess.cs       Mencari, membuka, dan menutup proses Claude Desktop
    ├── Arranger.cs            Monitor dan penempatan jendela (Win32)
    ├── CacheReaders.cs        Email akun, snapshot limit (Snappy + V8), salin setup
    ├── Models.cs              Model data, path, template layout
    ├── Native.cs              P/Invoke Win32
    ├── build.ps1              Build exe single-file + zip (x64 & ARM64)
    └── install.ps1            Install/uninstall per user (Start Menu, Desktop)
```

## Catatan privasi

- Semua data tetap di komputermu. Launcher tidak punya server, analytics, atau koneksi internet sama sekali.
- Dari folder data Claude, launcher hanya membaca: `config.json` (UUID akun), record akun di cache IndexedDB (untuk email), dan snapshot rate-limit. Token dan cookie login tidak pernah dibaca atau disalin.
- Folder `.claude-launcher\profiles\` berisi sesi login akunmu. Perlakukan seperti password.
