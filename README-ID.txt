YOUTUBE AUDIO PLAYER V3 — WINDOWS

KONSEP
Aplikasi desktop WinForms dengan UI pemutar musik tersendiri. Pencarian memakai YouTube Data API v3,
hasil tampil di daftar native, dan pemutaran menggunakan YouTube IFrame Player resmi dalam area
video yang tetap terlihat. Aplikasi tidak mengekstrak atau mengunduh audio.

FITUR
- UI mandiri: pencarian, daftar hasil, judul lagu, Play/Pause, Previous/Next, volume.
- Hanya satu WebView2 untuk player; tidak ada tab browser umum.
- Domain iklan/tracker pihak ketiga umum difilter.
- API key disimpan lokal pada Data/youtube-api-key.txt.
- Profil WebView2 lokal pada Data/WebViewProfile.

PERSYARATAN PENCARIAN
Buat API key YouTube Data API v3 di Google Cloud Console dan aktifkan YouTube Data API v3:
https://console.cloud.google.com/
Masukkan API key pada bagian bawah aplikasi, lalu klik Simpan API key.
API key memiliki kuota; pencarian memakai endpoint search.list yang mengonsumsi kuota API.

BATASAN IKLAN & PEMUTARAN
- Filter hanya domain pihak ketiga yang umum; tidak menjamin semua iklan terblokir.
- Iklan yang disajikan dari domain YouTube yang sama tidak diblokir oleh aplikasi.
- Pemutar resmi tetap terlihat sesuai persyaratan penggunaan player YouTube.
- Video tetap dirender oleh player resmi; menyembunyikan decoder atau memaksa audio-only tidak dilakukan.
- Beban CPU saat playback bergantung pada video, codec, resolusi, dan perangkat. UI aplikasi sendiri ringan,
  tetapi WebView2 + player YouTube tetap membutuhkan memori dan CPU.
- Tidak ada klaim bahwa konsumsi RAM/CPU akan selalu berada pada angka tertentu.

OPTIMASI
- Satu WebView2 saja, tanpa tab atau browser chrome.
- Tidak ada animasi UI atau gambar album tambahan.
- Pencarian memakai hasil teks.
- Timer pemeriksaan status player berjalan tiap 1,2 detik.
- WebView2 memakai profil lokal.

PORTABLE / RUNTIME
- Target Windows 10/11 x64.
- .NET 8 self-contained; Microsoft Edge WebView2 Evergreen Runtime tetap wajib tersedia.
  Runtime resmi: https://developer.microsoft.com/microsoft-edge/webview2/
- Salin seluruh folder aplikasi agar folder Data tetap bersama EXE.

BUILD EXE DENGAN GITHUB ACTIONS
1. Buat repository GitHub baru.
2. Upload semua file proyek termasuk .github/workflows.
3. Buka Actions > Build Windows portable EXE > Run workflow.
4. Download artifact YouTubeAudioPlayer-win-x64.
5. Jalankan YouTubeAudioPlayer.exe.

BUILD LOKAL (Windows + .NET 8 SDK)
dotnet publish YouTubeAudioPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish

CATATAN PENGUJIAN
Proyek sumber telah disusun, tetapi perlu dikompilasi dan diuji di Windows 10 x64. Ukuran RAM/CPU aktual
harus diukur di perangkat target saat idle dan saat memutar video.
