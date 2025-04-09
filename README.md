# Quadtree Image Compressor

## Deskripsi
**Quadtree** adalah struktur data hierarki yang digunakan untuk membagi ruang atau data menjadi bagian yang lebih kecil, yang sering digunakan dalam pengolahan gambar. Dalam konteks kompresi gambar, Quadtree membagi gambar menjadi blok-blok kecil berdasarkan keseragaman warna atau intensitas piksel. Prosesnya dimulai dengan membagi gambar menjadi empat bagian, lalu memeriksa apakah setiap bagian memiliki nilai yang seragam berdasarkan analisis sistem warna RGB, yaitu dengan membandingkan komposisi nilai merah (R), hijau (G), dan biru (B) pada piksel-piksel di dalamnya. Jika bagian tersebut tidak seragam, maka bagian tersebut akan terus dibagi hingga mencapai tingkat keseragaman tertentu atau ukuran minimum yang ditentukan.

Program ini akan:
- Membagi gambar ke dalam empat bagian secara rekursif.
- Mengukur keseragaman blok menggunakan metode analisis warna (RGB).
- Menghentikan pembagian jika blok sudah cukup seragam atau telah mencapai ukuran minimum blok yang sudah ditentukan.

Tujuan utama dari pembuatan program ini adalah untuk mengurangi ukuran gambar dengan mempertahankan kualitas visual.

## Requirements
- [.NET SDK](https://dotnet.microsoft.com/en-us/download)
- [SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp)

## Kompilasi program (Windows)
1. Buka command prompt dan pindah ke direktori project:
```bash
cd path\to\ImageCompressor
```

2. Jalankan perintah berikut untuk build .exe:

```bash
$ dotnet publish -c Release -r win-x64 --self-contained true
```

File executable akan berada di direktori
```bash
./bin/Release/netX.X/win-x64/publish/ImageCompressor.exe
```
(sesuaikan versi .NET, misal net9.0)

## Cara menjalankan program
1. Install [SixLabors](https://www.nuget.org/packages/SixLabors.ImageSharp) jika belum terinstal.
2. Pindah ke direktori file .exe berada
```bash
cd ./bin/Release/netX.X/win-x64/publish
```
3. Jalankan kode berikut

```bash
$ ImageCompressor.exe <imagePath> <errorMethod> <threshold> <minimumBlock> <outputPath>
```
contoh
```bash
$ ImageCompressor.exe "D:/stima/tucil_2/sample1.jpg" 1 2.0 16 "D:/stima/tucil_2/sample1_compressed.jpg"
```

Keterangan:
- imagePath: Path absolut file gambar berada
- errorMethod: Metode eror yang digunakan* 
- threshold: Ambang batas nilai eror
- minimumBlock: Ukuran blok minimum (> 4)
- outputPath: Path absolut file gambar hasil kompresi disimpan

*Berikut merupakan metode eror yang dapat dipilih
1: Variance ( > 0)
2: Mean Absolute Deviation (MAD) ( > 0)
3: Maximum Pixel Difference ( > 0)
4: Entropy ( > 0)
5: Structural Similarity Index (SSIM) ( 0 < SSIM < 1 )

# Author
Fardhan Indrayesa <br>
NIM: 12821046
