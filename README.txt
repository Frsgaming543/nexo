NexoIsland v1 (Windows 10/11, .NET 8) - jalankan: dotnet run
Developer: class_programmer/farez

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
  Pengaturan tersimpan di %APPDATA%\NexoIsland\settings.json

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
    * Data tersimpan di %APPDATA%\NexoIsland\events.json
- CUACA (baru): suhu sekarang, terasa seperti, kelembapan, angin + prakiraan 5 hari (peluang hujan).
  Sumber Open-Meteo (tanpa API key), kota mengikuti Pengaturan > kota jadwal shalat. Diperbarui tiap 20 menit.
- SISTEM (baru): cincin CPU, RAM, Disk (drive Windows), Baterai; kecepatan unduh/unggah; grafik CPU;
  lama komputer menyala. Hanya aktif menghitung saat halaman Sistem terbuka (hemat CPU).
- Tinggi island menyesuaikan tiap halaman.

BARU di v13
- MODE MUSIK (layar penuh), terinspirasi setup "Verci + Lumn":
    * buka: tombol 🖥 di halaman musik island | klik kanan island > Mode Musik | Pengaturan > MODE MUSIK > Buka
    * layar gelap dengan cahaya di tepi layar yang mengikuti warna cover dan BERDENYUT mengikuti level suara asli
      (peak meter Windows), plus bingkai neon tipis. Saat musik dijeda cahaya meredup.
    * lirik sinkron (LRCLIB) tampil besar: baris aktif terisi pelan sesuai waktu, baris lain meredup & bergeser naik.
    * tanpa lirik (atau tombol 💬/🖼 / tombol C) = tampilan cover besar + judul + artis.
    * panel pemutar melayang: cover kecil, judul, ⏮ ⏯ ⏭, bar progres (klik = pindah posisi, jika pemutar mendukung).
      Panel & kursor memudar otomatis kalau mouse diam 4,5 dtk.
    * tombol: Esc = tutup | Spasi = putar/jeda | ← → = lagu sebelumnya/berikutnya | C = ganti lirik/cover
    * island & salju tetap tampil di atas Mode Musik. Cahaya tepi dan jam bisa dimatikan di Pengaturan.
- LAMPU HIAS ANTAR JENDELA:
    * seret gulungan lampu, lepas di tepi atas jendela pertama -> ujung kabel terpasang. Gerakkan kursor (kabel
      menjuntai mengikuti kursor) lalu KLIK tepi atas jendela lain -> kabel terkait di antara dua jendela.
      Klik jendela yang sama = lampu di jendela itu saja (cara lama). Esc / klik kanan = batal.
    * kabel makin menjuntai kalau jaraknya jauh, ikut tertarik saat salah satu jendela digeser dan berayun.
      Jendela ditutup = kabel terlepas; diminimize / tertutup jendela lain = kabel disembunyikan sementara.
    * bisa dirangkai: A-B, lalu B-C, dst. Lepas satu kabel: tahan Shift + klik kanan di kabel.
      "Lepas semua lampu jendela" melepas semuanya. Mode lama (lampu hanya di satu jendela): matikan
      Pengaturan > LAMPU HIAS > Kaitkan lampu antar jendela.
- Cahaya tepi Mode Musik kini berupa bingkai yang MELINGKARI layar dan mengikuti lekuk dynamic island di tengah atas; dua "komet" cahaya berputar searah jarum jam, makin kencang mengikuti suara.
- Developer: class_programmer/farez (tercantum di Pengaturan > TENTANG, menu klik kanan island, info file .exe, dan Mode Musik).

BARU di v14
- PERBAIKAN lampu hias antar jendela: titik kait pertama sekarang mengikuti POSISI KURSOR saat gulungan dilepas
  (dulu memakai titik tengah gulungan, jadi kalau gulungan dipegang di sisi kanan, kabel malah kecantol di sisi kiri jendela).
- MODE CAHAYA tepi layar (Mode Musik): Komet, Denyut, Napas, Aurora, Pelangi, Neon, Ambient, Spektrum (batang suara di bawah).
- MODE LIRIK (Mode Musik): Klasik, Kode (diketik seperti editor kode + nomor baris), Konstelasi (kata = bintang yang
  terhubung garis, kata aktif dibidik cincin), Per kata (kata menyala satu-satu), Mesin ketik, Neon (tabung neon berkedip),
  Gelombang (huruf bergelombang mengikuti suara), Gulir (gaya daftar lirik, baris lain buram), Cover + teks.
- Ganti cepat: tombol G (cahaya) dan L (lirik), atau tombol ✨ dan Aa di panel pemutar. Pilihan juga ada di
  Pengaturan > MODE MUSIK dan tersimpan otomatis.

BARU di v15 (perbaikan)
- Mode Musik terbuka: salju di jendela, kostum ikon desktop, lampu hias & mainan DISEMBUNYIKAN (tidak lagi muncul di depan
  layar Mode Musik). Salju yang turun tetap tampil. Semuanya muncul lagi saat Mode Musik ditutup.
- Island tidak lagi tertutup Mode Musik: urutan layer dicek berkala & tiap Mode Musik diklik/diaktifkan, island dan salju dinaikkan lagi.
- Cahaya tepi Mode Musik sekarang MENGIKUTI BENTUK ISLAND secara langsung: saat island membesar, ditekan, atau berubah jadi
  HUD / notifikasi / lirik, lekuk bingkai ikut melebar dan menyesuaikan.
