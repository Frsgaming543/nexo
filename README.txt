SnowIsland v12 (Windows 10/11, .NET 8) - jalankan: dotnet run

Island: atas hitam, bawah gradient menyala (default biru; di halaman musik mengikuti warna cover lagu).
Bot mata + topi Natal: kursor bergerak = mengikuti | hover = senang | diam 4 dtk = ngopi (atau goyang kalau ada musik)
  diam 12 dtk = ngantuk | diam 17 dtk = tidur (zzz) | gerakkan kursor = kaget & bangun

BARU di v6
- HUD di island: Caps Lock, Num Lock, volume naik/turun (dengan bar & persen), mute / suara aktif
- Panah ‹ › di header untuk pindah halaman (musik, shalat, fokus, to-do), dengan animasi geser
- Ikon fase hari di samping jam: malam, pagi, pagi->siang, siang, siang->sore, sore, sore->malam
- Jendela Pengaturan terpisah: klik ikon ⚙ di header island, atau klik kanan island > Pengaturan...
    * gradient on/off, warna utama (preset / slider RGB / kode hex), opsi 2 warna, ikuti cover lagu, glow
    * salju, bot mata, topi Natal, ikon waktu on/off
    * notifikasi per jenis on/off (WiFi, Bluetooth, shalat, baterai, Caps/Num Lock, volume, terminal, fokus, Natal)
    * kota & negara untuk jadwal shalat
  Pengaturan tersimpan di %APPDATA%\SnowIsland\settings.json

Tab 🎵 musik, 🕌 jadwal shalat (sun arc), ⏱ fokus (Pomodoro), ✅ to-do

BARU di v7 (Natal)
- Salju menutupi SEMUA monitor (bukan hanya layar utama), tetap bisa didorong kursor & klik = ledakan salju
- Salju di tepi atas setiap jendela & taskbar (seperti di video macOS): menumpuk pelan, ada es menggantung
    * gerakkan kursor di tepi jendela = salju tersapu & beterbangan | klik = disekop | lalu tumbuh lagi
    * salju yang turun mendarat dan menambah tumpukan | tertutup jendela lain = otomatis tersembunyi
- Ikon desktop memakai kostum Natal unik (11 gaya): Santa, bauble, kado, wreath, candy cane, snow globe,
  gingerbread, lampu kedip, reindeer, frost, elf. Ikon asli tetap di dalamnya.
    * hover = melompat | klik = goyang + kilau | tertutup jendela = memudar
    * posisi mengikuti ikon yang digeser; ikon baru otomatis mendapat kostum berikutnya
- Pengaturan > Tampilan: Salju, Salju di jendela, Ikon desktop Natal bisa dinyalakan terpisah
Catatan: jika monitor punya skala DPI berbeda, posisi ikon di monitor kedua bisa sedikit meleset.

BARU di v8
- Ikon desktop: gambar aplikasi ASLI tetap terlihat. Kostum Natal sekarang berupa bingkai + aksesori
  di sekeliling ikon (bagian tengah bingkai berlubang), bukan menggambar ulang ikon.
- Salju jatuh: kursor menyapu tumpukan / klik menyekop / jendela DIGOYANG (geser bolak-balik cepat)
  -> salju rontok jadi gumpalan yang jatuh, dan menambah tumpukan di jendela di bawahnya.
- Mainan (Pengaturan > MAINAN): buldoser, kereta Santa, pohon Natal, kado, dan manusia salju susun-sendiri.
    * seret = pindahkan | klik buldoser = jalan/berhenti (mendorong salju) | klik kereta Santa = terbang/mendarat
    * klik kanan = hapus mainan
    * tumpuk bola besar > sedang > kecil; lepas mata/hidung/topi/syal/tangan/kancing dekat bola untuk menempelkan
    * mainan berdiri di atas salju jendela/taskbar dan ikut melompat kalau jendelanya digoyang

BARU di v9
- Salju turun lebih realistis: 3 lapis kedalaman (jauh kecil & lambat, dekat besar & cepat), tepi lembut,
  bokeh buram, angin + gust yang datang dan pergi, serpihan melayang kiri-kanan.
- Jendela DITUTUP / diminimize / di-maximize -> seluruh salju di tepinya rontok jatuh ke bawah
  (mendarat di jendela lain di bawahnya).
- Bola salju buatan sendiri: tahan Ctrl (atau Pengaturan > MAINAN > Mode gulung bola salju) lalu geser kursor
  di atas tumpukan salju / salju yang turun. Bola terbentuk di kursor & makin besar; lepas Ctrl / kursor diam = bola jadi.
  Bola bisa ditumpuk & diberi mata, hidung, topi, dst.

BARU di v10
- Jendela digeser (kanan/kiri/atas/bawah) -> tumpukan salju tumpah ke bawah (makin cepat digeser, makin banyak),
  tidak lagi tertinggal melayang. Salju baru menumpuk lagi setelah jendela diam.
- Daftar jendela dibaca di thread terpisah + serpihan salju memakai transform (lebih ringan), jadi salju
  mengikuti jendela tanpa lag. Jendela hantu/transparan (mis. pratinjau seret tab Chrome) tidak diberi salju.

BARU di v11
- Jendela digeser: salju di tepinya LANGSUNG lepas dan jatuh (membawa sedikit laju jendela), lapisan salju
  disembunyikan selama jendela bergerak, salju yang turun tidak mendarat di jendela yang sedang bergerak,
  dan salju baru tumbuh pelan setelah jendela diam. Tidak ada lagi tumpukan yang melayang.
- Bola salju OTOMATIS (tanpa menu / tanpa Ctrl): geser kursor kiri-kanan di atas tumpukan salju jendela ->
  bola terbentuk di kursor, makin besar selama menyapu salju. KLIK MOUSE = bola dilepas. Bisa dimatikan di
  Pengaturan > MAINAN > Bola salju otomatis. (Tombol bola & Mode gulung di menu dihapus.)
- Buldoser sekarang MENDORONG salju: pisau mengumpulkan salju jadi gundukan yang ikut berjalan; sampai tepi
  jendela gundukan jatuh ke bawah (menambah tumpukan di jendela di bawahnya). Makin berat muatan, makin pelan.
- Semua mainan berdiri di atas jendela dan ikut terbawa saat jendela digeser.
- Pohon Natal baru: besar (~280 px), 6 tingkat cabang, bayangan, salju di cabang, bola hias, bintang.
- LAMPU HIAS (Pengaturan > LAMPU HIAS):
    * klik "Lampu hias" lalu seret gulungannya. Lepas di dekat pohon = pohon menyala.
    * lepas di tepi atas jendela = kabel lampu terpasang di jendela itu & ikut bergerak bersama jendela.
    * lepas lampu dari jendela: tahan Shift + klik kanan di kabel (atau "Lepas semua lampu jendela").
    * diatur: warna (hangat / warna-warni / dingin / pink / merah-hijau), pola (diam / kedip / berjalan /
      berkelip halus), kecepatan, jarak antar lampu, kabel kendur. Berlaku untuk lampu pohon & jendela.

BARU di v12 (UI island lebih modern + halaman baru)
- DOCK navigasi di bagian bawah island: pil ikon, halaman aktif membesar & menampilkan namanya.
  Panah ‹ › di header tetap bisa dipakai. Judul halaman tampil di kiri header.
- TUGAS (baru): cincin progres + persen, input bergaya pil dengan tombol +, tiga level prioritas
  (klik titik di kolom input / titik di tiap tugas untuk ganti: biasa / penting / mendesak),
  centang animasi, tugas selesai turun ke bawah, hover = tombol hapus, "Bersihkan selesai".
- FOKUS (baru): cincin besar dengan penanda, tombol segmented Fokus / Istirahat, 3 preset terlihat sekaligus
  (25·5, 50·10, 15·3), pilih TUGAS yang difokuskan dari daftar tugas (klik untuk ganti, ✓ untuk menandai selesai),
  statistik hari ini (sesi & menit, tersimpan di focus.json), titik siklus 4 sesi.
  Saat island tertutup: cincin mini + sisa waktu + nama tugas.
- KALENDER (baru): kalender bulanan (minggu mulai Senin), titik berwarna di tanggal yang punya acara.
    * klik tanggal -> daftar acara tanggal itu + kolom tambah acara (langsung fokus untuk mengetik)
    * ketik "19:30 Rapat" untuk acara berjam, atau cukup "Ulang tahun Ani" untuk acara seharian
    * klik titik warna di kolom input untuk ganti warna acara | hover kartu acara = tombol hapus
    * tombol ‹ ● › : bulan sebelumnya / kembali ke hari ini / bulan berikutnya
    * Pengingat: ringkasan acara hari ini (atau besok) beberapa detik setelah island dibuka,
      dan notifikasi tepat saat jam acara tiba. Bisa dimatikan: Pengaturan > Notifikasi > Pengingat kalender.
    * Data tersimpan di %APPDATA%\SnowIsland\events.json
- CUACA (baru): suhu sekarang, terasa seperti, kelembapan, angin + prakiraan 5 hari (peluang hujan).
  Sumber Open-Meteo (tanpa API key), kota mengikuti Pengaturan > kota jadwal shalat. Diperbarui tiap 20 menit.
- SISTEM (baru): cincin CPU, RAM, Disk (drive Windows), Baterai; kecepatan unduh/unggah; grafik CPU;
  lama komputer menyala. Hanya aktif menghitung saat halaman Sistem terbuka (hemat CPU).
- Tinggi island menyesuaikan tiap halaman.
