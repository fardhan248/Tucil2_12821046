using System;
using System.IO;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;

class Program
{
	static int nodes = 1; // root, untuk total simpul pohon
	static string ext = "";
	
	static void Main(string[] args) // {imPath, errorMethod, threshold, minblock, outPath}
	{
		if (args.Length < 5)
		{
			Console.WriteLine("Argumen tidak lengkap. Gunakan format <imPath> <errorMethod> <threshold> <minBlock> <outPath>");
			return;
		}
		
		if (!File.Exists(args[0]))
		{
			Console.WriteLine("File gambar tidak ditemukan!");
			return;
		}
		
		string imPath = args[0];
		int errorMethod = int.Parse(args[1]);
		double threshold = double.Parse(args[2]);
		int minBlock = int.Parse(args[3]);
		string outPath = args[4];
		ext = Path.GetExtension(imPath).ToLower();
		int deep;
		
		if (minBlock < 0)
		{
			Console.WriteLine("Ukuran minimum blok tidak valid! Masukkan angka > 4");
			return;
		}
		
		if ((errorMethod < 1) || (errorMethod > 5))
		{
			Console.WriteLine("Metode eror tidak valid! Masukkan angka 1 sampai 5");
			return;
		}
		
		if (threshold < 0)
		{
			Console.WriteLine("Threshold tidak valid. Masukkan threshold > 0");
			return;
		}
		else if ((errorMethod == 5) && (threshold > 1))
		{
			Console.WriteLine("Threshold tidak valid. Untuk SSIM, masukkan 0 < threshold < 1");
			return;
		}
		
		using (Image<Rgba32> image = Image.Load<Rgba32>(imPath))
		{
			int w = image.Width;
			int h = image.Height;
			int[,,] matrixIm = new int[h, w, 3];  // matriks RGB
			int[,] a = new int[h, w];  // simpan nilai alpha untuk png, apabila terdapat piksel transparan
			
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Rgba32 px = image[x, y];
					matrixIm[y, x, 0] = px.R;
					matrixIm[y, x, 1] = px.G;
					matrixIm[y, x, 2] = px.B;
					a[y, x] = px.A;
				}
			}
			Console.WriteLine($"Gambar dimuat dengan ukuran: {w}*{h} px");
			
			var fileInfo = new FileInfo(imPath);
			double sizeMB = fileInfo.Length / (1024.0*1024.0);  // Ukuran awal gambar

			int height = matrixIm.GetLength(0);
			int width = matrixIm.GetLength(1);
			int channel = matrixIm.GetLength(2);
			
			Stopwatch duration = new Stopwatch();
			Console.WriteLine("Mengkompres gambar...");
			if (errorMethod == 5)
			{
				int[,,] Q = FillArray(matrixIm, 0, 0, height, width);  // untuk SSIM, Q sebagai matriks RGB yang tidak diubah
				
				duration.Start();
				deep = dnc(matrixIm, errorMethod, threshold, minBlock, 0, 0, height, width, Q);
				duration.Stop();
			}
			else
			{
				duration.Start();
				deep = dnc(matrixIm, errorMethod, threshold, minBlock, 0, 0, height, width);
				duration.Stop();
			}
			
			long ms = duration.ElapsedMilliseconds;
			double seconds = ms / 1000;             // Konversi waktu eksekusi ke detik
			
			double lastSize = ToImage(matrixIm, outPath, a);  // Konversi gambar dari matriks RGB ke file gambar
			Console.WriteLine($"Waktu eksekusi: {seconds:F2} detik");
			Console.WriteLine($"Ukuran gambar sebelum di-compress: {sizeMB:F2} MB");
			Console.WriteLine($"Ukuran gambar setelah di-compress: {lastSize:F2} MB");
			Console.WriteLine($"Persentase kompresi: {CompressPercent(sizeMB, lastSize):F2}%");
			Console.WriteLine("Kedalaman pohon: " + deep);
			Console.WriteLine("Banyak simpul: " + nodes);
			Console.WriteLine($"Gambar tersimpan di {outPath}");
		}
	}
	
	// Algoritma divide and conquer Quadtree
	public static int dnc(int[,,] P, int errorMethod, double thresH, int minblock, int i, int j, int height, int width, int[,,]? Q = null)
	{
		int depth;  // kedalaman pohon
		int x = i, y = j;
		int N = height * width;
		double error, sim;
		
		if (errorMethod == 5)  // SSIM
		{
			Q ??= P;
			int[,,] PSliced = FillArray(P, x, y, height, width);
			int[,,] QSliced = FillArray(Q, x, y, height, width);
			
			// Hitung rata-rata tiap channel
			double rpSum = 0, rpMean = 0;
			double gpSum = 0, gpMean = 0;
			double bpSum = 0, bpMean = 0;
			for (int ii = 0; ii < width; ii++)
			{
				for (int jj = 0; jj < height; jj++)
				{
					rpSum += PSliced[jj, ii, 0]; 
					gpSum += PSliced[jj, ii, 1];
					bpSum += PSliced[jj, ii, 2];
				}
			}
			rpMean = rpSum / N;
			gpMean = gpSum / N;
			bpMean = bpSum / N;
			
			for (int ii = 0; ii < PSliced.GetLength(1); ii++)
			{
				for (int jj = 0; jj < PSliced.GetLength(0); jj++)
				{
					PSliced[jj, ii, 0] = (int)rpMean;
					PSliced[jj, ii, 1] = (int)gpMean;
					PSliced[jj, ii, 2] = (int)bpMean;
				}
			}
			
			sim = ErrorMeasurement(errorMethod, PSliced, QSliced);
			
			if (sim > thresH)  // berhenti apabila similarity melebihi threshold
			{
				// Isi dengan rata-rata blok
				for (int ii = x; ii < width + x; ii++)
				{
					for (int jj = y; jj < height + y; jj++)
					{
						P[jj, ii, 0] = (int)rpMean;
						P[jj, ii, 1] = (int)gpMean;
						P[jj, ii, 2] = (int)bpMean;
					}
				}	
				return 1;
			}
			else if (N < minblock)  // berhenti apabila jumlah piksel melebihi minblock
			{
				// Isi dengan rata-rata blok
				for (int ii = x; ii < width + x; ii++)
				{
					for (int jj = y; jj < height + y; jj++)
					{
						P[jj, ii, 0] = (int)rpMean;
						P[jj, ii, 1] = (int)gpMean;
						P[jj, ii, 2] = (int)bpMean;
					}
				}	
				return 1;
			}
			else
			{
				// bagi blok menjadi empat bagian
				int heightDiv1 = height / 2;
				int heightDiv2 = height - heightDiv1;
				
				int widthDiv1 = width / 2;
				int widthDiv2 = width - widthDiv1;
				
				// Selesaikan masing-masing blok
				int d1 =  dnc(P, errorMethod, thresH, minblock, x, y, heightDiv1, widthDiv1, Q);
				nodes += 1;
				int d2 =  dnc(P, errorMethod, thresH, minblock, x+widthDiv1, y, heightDiv1, widthDiv2, Q);
				nodes += 1;
				int d3 =  dnc(P, errorMethod, thresH, minblock, x, y+heightDiv1, heightDiv2, widthDiv1, Q);
				nodes += 1;
				int d4 =  dnc(P, errorMethod, thresH, minblock, x+widthDiv1, y+heightDiv1, heightDiv2, widthDiv2, Q);
				nodes += 1;
				
				depth = 1 + Math.Max(Math.Max(d1, d2), Math.Max(d3, d4));
				return depth;
			}
		}
		else  // selain SSIM
		{
			int[,,] PSliced = FillArray(P, x, y, height, width);
			
			// Hitung rata-rata tiap channel
			double rSum = 0, rMean = 0;
			double gSum = 0, gMean = 0;
			double bSum = 0, bMean = 0;
			for (int ii = 0; ii < width; ii++)
			{
				for (int jj = 0; jj < height; jj++)
				{
					rSum += PSliced[jj, ii, 0];
					gSum += PSliced[jj, ii, 1];
					bSum += PSliced[jj, ii, 2];
				}
			}
			rMean = rSum /N;
			gMean = gSum /N;
			bMean = bSum /N;
			
			error = ErrorMeasurement(errorMethod, PSliced);
			
			if (error < thresH)  // berhenti apabila error kurang dari threshold
			{				
				for (int ii = x; ii < width + x; ii++)
				{
					for (int jj = y; jj < height + y; jj++)
					{
						P[jj, ii, 0] = (int)rMean;
						P[jj, ii, 1] = (int)gMean;
						P[jj, ii, 2] = (int)bMean;
					}
				}	
				return 1;
			}
			else if (N < minblock)  // berhenti apabila jumlah piksel melebihi minblock
			{
				for (int ii = x; ii < width + x; ii++)
				{
					for (int jj = y; jj < height + y; jj++)
					{
						P[jj, ii, 0] = (int)rMean;
						P[jj, ii, 1] = (int)gMean;
						P[jj, ii, 2] = (int)bMean;
					}
				}	
				return 1;
			}
			else
			{	
				// bagi blok menjadi empat bagian
				int heightDiv1 = height / 2;
				int heightDiv2 = height - heightDiv1;
				
				int widthDiv1 = width / 2;
				int widthDiv2 = width - widthDiv1;
				
				// Selesaikan masing-masing blok
				int d1 =  dnc(P, errorMethod, thresH, minblock, x, y, heightDiv1, widthDiv1);
				nodes += 1;
				int d2 =  dnc(P, errorMethod, thresH, minblock, x+widthDiv1, y, heightDiv1, widthDiv2);
				nodes += 1;
				int d3 =  dnc(P, errorMethod, thresH, minblock, x, y+heightDiv1, heightDiv2, widthDiv1);
				nodes += 1;
				int d4 =  dnc(P, errorMethod, thresH, minblock, x+widthDiv1, y+heightDiv1, heightDiv2, widthDiv2);
				nodes += 1;

				depth = 1 + Math.Max(Math.Max(d1, d2), Math.Max(d3, d4));
				return depth;
			}
		}
	}
	
	// Fungsi untuk konversi matriks RGB menjadi file gambar
	public static double ToImage(int[,,] P, string outputPath, int[,] alpha)
	{
		int height = P.GetLength(0);
		int width = P.GetLength(1);
		double sizeMB;
		
		using (Image<Rgba32> image = new Image<Rgba32>(width, height))
		{
			for (int j = 0; j < height; j++)
			{
				for (int i = 0; i < width; i++)
				{
					int r = P[j, i, 0];
					int g = P[j, i, 1];
					int b = P[j, i, 2];
					
					if (ext == ".png")
					{
						int a = alpha[j, i];
						image[i, j] = new Rgba32((byte)r, (byte)g, (byte)b, (byte)a);
					}
					else
					{
						image[i, j] = new Rgba32((byte)r, (byte)g, (byte)b);
					}
				}
			}
			image.Save(outputPath);
			
			IImageEncoder encoder = ext == ".png" ? new PngEncoder() : new JpegEncoder();
			
			// Mengambil ukuran gambar (dalam MB) setelah di-compress
			using var memoryStream = new MemoryStream();
			image.Save(memoryStream, encoder);
			long size = memoryStream.Length;
			sizeMB = size / (1024.0*1024.0);
		}
		return sizeMB; 
	}
	
	// Fungsi untuk meng-copy array untuk satu blok
	public static int[,,] FillArray(int[,,] P, int istart, int jstart, int height, int width)
	{
		int[,,] PSliced = new int[height, width, P.GetLength(2)];
		
		for (int i = 0; i < P.GetLength(2); i++)
		{
			for (int j = istart; j < istart+width; j++)
			{
				for (int k = jstart; k < jstart+height; k++)
					PSliced[k-jstart, j-istart, i] = P[k, j, i];
			}
		}
		return PSliced;
	}
	
	// Fungsi logaritma dengan basis 2
	public static double log2<T>(T x) where T : struct
	{
		return Math.Log(Convert.ToDouble(x)) / Math.Log(2);
	}
	
	// Fungsi untuk menghitung persen gambar yang sudah dikompresi
	public static double CompressPercent(double firstByte, double secondByte)
	{
		return (1 - (secondByte / firstByte)) * 100;
	}
	
	// Perhitungan eror untuk satu blok gambar
	public static double ErrorMeasurement(int method, int[,,] P, int[,,]? Q = null)
	{
		if (method == 1)  // Variance
		{
			int height = P.GetLength(0);  // tinggi piksel dalam satu blok
			int width = P.GetLength(1);  // lebar piksel dalam satu blok
			int channel = P.GetLength(2);
			int N = width * height;  // banyak piksel dalam satu blok
			double varianSum = 0;
			double miu;
			double sum;  
			double varRGB;
			
			for (int i = 0; i < channel; i++)
			{
				// Menghitung rata-rata
				sum = 0;
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						sum += P[k, j, i];
					}
				}
				
				miu = sum / N;
				
				// Menghitung variansi dalam satu channel
				sum = 0;
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						sum += Math.Pow((P[k, j, i] - miu), 2);
					}
				}
				
				varianSum += sum / N;
			}
			
			varRGB = varianSum / 3;  // Rata-rata variansi RGB
			
			return varRGB;
		}
		else if (method == 2)  // MAD
		{
			int height = P.GetLength(0);  // tinggi piksel dalam satu blok
			int width = P.GetLength(1);  // lebar piksel dalam satu blok
			int channel = P.GetLength(2);
			int N = width * height;  // banyak piksel dalam satu blok
			double madSum = 0;
			double sum;
			double miu;
			double madRGB;
			
			for (int i = 0; i < channel; i++)
			{
				// Menghitung rata-rata 
				sum = 0;
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						sum += P[k, j, i];
					}
				}
				
				miu = sum / N;
				
				// Menghitung jumlah MAD dalam satu channel
				sum = 0;
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						sum += Math.Abs(P[k, j, i] - miu);
					}
				}
				
				madSum += sum / N;
			}
			
			madRGB = madSum / 3;  // Rata-rata MAD RGB
			
			return madRGB;
		}
		else if (method == 3)  // Max Pixel Difference
		{
			int height = P.GetLength(0);  // tinggi piksel dalam satu blok
			int width = P.GetLength(1);  // lebar piksel dalam satu blok
			int channel = P.GetLength(2);
			double dSum = 0;
			int maks;
			int min;
			double dRGB;
			double dc;
			
			for (int i = 0; i < channel; i++)
			{
				// Mencari nilai maks dan min dalam satu blok
				maks = P[0, 0, i];
				min = P[0, 0, i];
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						if (maks < P[k, j, i])
						{
							maks = P[k, j, i];
						}
						if (min > P[k, j, i])
						{
							min = P[k, j, i];
						}
					}
				}
				dc = maks - min;
				dSum += dc;
			}
			dRGB = dSum / 3;  // Rata-rata MPD RGB
			
			return dRGB;
		}
		else if (method == 4)  // Entropy
		{
			int height = P.GetLength(0);  // tinggi piksel dalam satu blok
			int width = P.GetLength(1);  // lebar piksel dalam satu blok
			int channel = P.GetLength(2);
			int N = width * height;  // banyak piksel dalam satu blok
			double hSum = 0;
			double hRGBSum = 0;
			double prob;
			double hRGB;
			int[,] p = new int[height, width];
			
			for (int i = 0; i < channel; i++)
			{
				Dictionary<int, int> valueCount = new Dictionary<int, int>{};
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						p[k, j] = P[k, j, i];
					}
				}
				
				// Mendapatkan jumlah nilai piksel dalam satu blok
				foreach (int val in p)
				{
					if (valueCount.ContainsKey(val))
					{
						valueCount[val]++;
					}
					else
					{	
						valueCount[val] = 1;
					}
				}
				
				foreach (var values in valueCount)
				{
					prob = (double)values.Value / N;  // Hitung probabilitas kemunculan nilai piksel dalam satu blok
					hSum -= prob * log2(prob);
				}
				
				hRGBSum += hSum;
			}
			
			hRGB = hRGBSum / 3;  // Rata-rata entropi RGB
			return hRGB; 
		}
		else if (method == 5)  // SSIM
		{
			Q ??= P;
			
			int height = P.GetLength(0);  // tinggi piksel dalam satu blok
			int width = P.GetLength(1);  // lebar piksel dalam satu blok
			int channel = P.GetLength(2);
			int N = width * height;  // banyak piksel dalam satu blok
			double pSum, qSum, pqSum;
			double pMean, qMean;
			double pVar, qVar, pqVar;
			double ssim = 0, ssimC;
			double C1, C2, K1 = 0.01, K2 = 0.03;  // (Zhou Wang, dkk; 2004)
			double[] w = {0.299, 0.587, 0.114};  // ITU-R BT.601-7
			double l, cs;
			
			for (int i = 0; i < channel; i++)
			{
				pSum = 0;
				qSum = 0;
				// mean
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						pSum += P[k, j, i];
						qSum += Q[k, j, i];
					}
				}
				pMean = pSum / N;
				qMean = qSum / N;
				
				// covariance and variance
				pSum = 0;
				qSum = 0;
				pqSum = 0;
				for (int j = 0; j < width; j++)
				{
					for (int k = 0; k < height; k++)
					{
						pSum += Math.Pow((P[k, j, i] - pMean), 2);			   // variance P
						qSum += Math.Pow((Q[k, j, i] - qMean), 2);			   // variance Q
						pqSum += (P[k, j, i] - pMean) * (Q[k, j, i] - qMean);  // covariance PQ
					}
				}
				pVar = pSum / (N - 1);
				qVar = qSum / (N - 1);
				pqVar = pqSum / (N - 1);
				
				C1 = Math.Pow(K1*255, 2);  // 255 -> RGB 24-bit @ 8-bit per channel
				C2 = Math.Pow(K2*255, 2);
				
				l = (2*pMean*qMean + C1) / (Math.Pow(pMean, 2) + Math.Pow(qMean, 2) + C1);
				cs = (2*pqVar + C2) / (pVar + qVar + C2);
				ssimC = l*cs;
				
				ssim += w[i] * ssimC;
			}
			return ssim;
		}		
		return 0.0;
	}	
}