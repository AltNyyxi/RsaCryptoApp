using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RsaCryptoApp
{
    public partial class MainWindow : Window
    {
        private string _selectedFilePath = null;

        private long _p = 0, _q = 0, _r = 0, _phi = 0, _e = 0, _d = 0;

        public MainWindow()
        {
            InitializeComponent();
            UpdateMode();
        }

        // ─── БЫСТРОЕ ВОЗВЕДЕНИЕ В СТЕПЕНЬ ПО МОДУЛЮ ───────────────────────────────

        private static long FastExp(long a, long z, long n)
        {
            if (n == 1) return 0;
            long a1 = a % n;
            long z1 = z;
            long x = 1;

            while (z1 != 0)
            {
                // Пока z1 чётное — делим пополам, возводим основание в квадрат
                while (z1 % 2 == 0)
                {
                    z1 /= 2;
                    a1 = MulMod(a1, a1, n);
                }
                z1 -= 1;
                x = MulMod(x, a1, n);
            }
            return x;
        }

        private static long MulMod(long a, long b, long n)
        {
            return (a % n) * (b % n) % n;
        }

        // ─── РАСШИРЕННЫЙ АЛГОРИТМ ЕВКЛИДА ─────────────────────────────────────────
        // Возвращает НОД(a,b) и коэффициенты x,y такие что x*a + y*b = НОД(a,b)
        private static long ExtendedGcd(long a, long b, out long x, out long y)
        {
            long d0 = a, d1 = b;
            long x0 = 1, x1 = 0;
            long y0 = 0, y1 = 1;

            while (d1 > 0)
            {
                long q = d0 / d1;
                long d2 = d0 % d1;
                long x2 = x0 - q * x1;
                long y2 = y0 - q * y1;

                d0 = d1; d1 = d2;
                x0 = x1; x1 = x2;
                y0 = y1; y1 = y2;
            }

            x = x0;
            y = y0;
            return d0; // НОД
        }

        // ─── ВЫЧИСЛЕНИЕ МУЛЬТИПЛИКАТИВНОГО ОБРАТНОГО ──────────────────────────────
        // Находит d такое что (e * d) mod phi == 1
        // Использует расширенный алгоритм Евклида: y1 * e ≡ 1 (mod phi)
        private static long ModInverse(long e, long phi)
        {
            long x, y;
            long gcd = ExtendedGcd(phi, e, out x, out y);
            if (gcd != 1)
                throw new Exception($"НОД(e, φ(r)) = {gcd} ≠ 1. Числа e и φ(r) не взаимно просты!");
            long d = y % phi;
            if (d < 0) d += phi;
            return d;
        }

        // ─── ПРОВЕРКА ПРОСТОТЫ ─────────────────────────────────────────────────────
        private static bool IsPrime(long n)
        {
            if (n < 2) return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (long i = 3; i * i <= n; i += 2)
                if (n % i == 0) return false;
            return true;
        }

        // ─── ВЫЧИСЛЕНИЕ НОД ────────────────────────────────────────────────────────
        private static long Gcd(long a, long b)
        {
            while (b != 0) { long t = b; b = a % b; a = t; }
            return a;
        }

        // ─── ВАЛИДАЦИЯ И ВЫЧИСЛЕНИЕ ПАРАМЕТРОВ ────────────────────────────────────
        private bool TryComputeParams(out string error)
        {
            error = "";
            _p = _q = _r = _phi = _e = _d = 0;

            if (rbDecrypt.IsChecked == true)
            {
                if (!long.TryParse(txtR.Text.Trim(), out _r) || _r < 4)
                { error = "Введите корректное значение r (целое ≥ 4)"; return false; }

                if (!long.TryParse(txtD.Text.Trim(), out _d) || _d < 1)
                { error = "Введите корректное значение d (закрытый ключ, целое ≥ 1)"; return false; }

                return true;
            }

            // ── РЕЖИМ ШИФРОВАНИЯ ──
            if (!long.TryParse(txtP.Text.Trim(), out _p))
            { error = "p: введите целое число"; return false; }
            if (!IsPrime(_p))
            { error = $"p = {_p} не является простым числом!"; return false; }
            if (_p < 3)
            { error = "p должно быть простым числом ≥ 3"; return false; }
            if (_p >= 32768)
            { error = "p должно быть < 32768 (ограничение long-арифметики без BigInteger)"; return false; }

            if (!long.TryParse(txtQ.Text.Trim(), out _q))
            { error = "q: введите целое число"; return false; }
            if (!IsPrime(_q))
            { error = $"q = {_q} не является простым числом!"; return false; }
            if (_q < 3)
            { error = "q должно быть простым числом ≥ 3"; return false; }
            if (_q >= 32768)
            { error = "q должно быть < 32768 (ограничение long-арифметики без BigInteger)"; return false; }
            if (_q == _p)
            { error = "p и q не должны совпадать!"; return false; }

            _r = _p * _q;
            if (_r <= 255)
            { error = $"r = p×q = {_r} должно быть > 255 (для шифрования байтов 0..255)"; return false; }

            _phi = (_p - 1) * (_q - 1);

            if (!long.TryParse(txtE.Text.Trim(), out _e))
            { error = "e: введите целое число"; return false; }
            if (_e <= 1 || _e >= _phi)
            { error = $"e должно быть в диапазоне (1, φ(r)) = (1, {_phi})"; return false; }
            if (Gcd(_e, _phi) != 1)
            { error = $"НОД(e={_e}, φ(r)={_phi}) ≠ 1. Выберите другое e!"; return false; }

            try { _d = ModInverse(_e, _phi); }
            catch (Exception ex) { error = ex.Message; return false; }

            return true;
        }

        // ─── ОБНОВЛЕНИЕ ПОЛЕЙ ПРИ ВВОДЕ ПАРАМЕТРОВ ────────────────────────────────
        private void ParamChanged(object sender, TextChangedEventArgs e)
        {
            if (txtP == null || txtQ == null) return;
            RefreshComputedFields();
        }

        private void RefreshComputedFields()
        {
            txtError.Text = "";
            txtR.IsReadOnly = true;
            txtD.IsReadOnly = true;

            if (rbDecrypt?.IsChecked == true)
            {
                return;
            }

            if (long.TryParse(txtP.Text.Trim(), out long p) &&
                long.TryParse(txtQ.Text.Trim(), out long q) &&
                IsPrime(p) && IsPrime(q) && p != q && p >= 3 && q >= 3 && p < 32768 && q < 32768)
            {
                long r = p * q;
                long phi = (p - 1) * (q - 1);
                txtR.Text = r.ToString();
                txtPhi.Text = phi.ToString();

                if (long.TryParse(txtE.Text.Trim(), out long eVal) &&
                    eVal > 1 && eVal < phi && Gcd(eVal, phi) == 1)
                {
                    try
                    {
                        long d = ModInverse(eVal, phi);
                        txtD.Text = d.ToString();
                    }
                    catch { txtD.Text = "—"; }
                }
                else
                {
                    txtD.Text = "—";
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(txtP.Text) || !string.IsNullOrWhiteSpace(txtQ.Text))
                {
                    txtR.Text = "—";
                    txtPhi.Text = "—";
                    txtD.Text = "—";
                }
            }

            UpdateProcessButton();
        }

        // ─── СМЕНА РЕЖИМА ─────────────────────────────────────────────────────────
        private void ModeChanged(object sender, RoutedEventArgs e)
        {
            UpdateMode();
        }

        private void UpdateMode()
        {
            if (txtP == null) return;

            bool isEncrypt = rbEncrypt?.IsChecked == true;

            // В режиме шифрования: p,q,e вводятся; r,phi,d — авто
            // В режиме дешифрования: r и d вводятся вручную; остальное не нужно
            txtP.IsReadOnly = !isEncrypt;
            txtQ.IsReadOnly = !isEncrypt;
            txtE.IsReadOnly = !isEncrypt;

            if (isEncrypt)
            {
                txtR.IsReadOnly = true;
                txtPhi.IsReadOnly = true;
                txtD.IsReadOnly = true;

                lblResult.Text = "ЗАШИФРОВАННЫЙ ФАЙЛ (16-бит блоки в dec)";
                btnProcess.Content = "🔒 Зашифровать";
            }
            else
            {
                txtR.IsReadOnly = false;
                txtPhi.IsReadOnly = true;
                txtD.IsReadOnly = false;

                txtPhi.Text = "—";
                txtP.Text = "";
                txtQ.Text = "";
                txtE.Text = "";

                lblResult.Text = "РАСШИФРОВАННЫЙ ФАЙЛ (байты в dec)";
                btnProcess.Content = "🔓 Расшифровать";
            }

            memoSource.Clear();
            memoResult.Clear();
            txtError.Text = "";
            UpdateProcessButton();
        }

        private void UpdateProcessButton()
        {
            if (btnProcess == null) return;
            btnProcess.IsEnabled = (_selectedFilePath != null);
        }

        // ─── ОТКРЫТЬ ФАЙЛ ─────────────────────────────────────────────────────────
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите файл для обработки",
                Filter = "Все файлы (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                string name = Path.GetFileName(_selectedFilePath);
                long size = new FileInfo(_selectedFilePath).Length;
                txtStatus.Text = $"📄 {name}  |  {size} байт  |  {_selectedFilePath}";

                LoadSourceFile();
                memoResult.Clear();
                UpdateProcessButton();
            }
        }

        private void LoadSourceFile()
        {
            if (_selectedFilePath == null) return;
            try
            {
                byte[] data = File.ReadAllBytes(_selectedFilePath);
                int showCount = Math.Min(data.Length, 2048);
                var sb = new StringBuilder();
                for (int i = 0; i < showCount; i++)
                {
                    sb.Append(data[i]);
                    if (i < showCount - 1) sb.Append(' ');
                    if ((i + 1) % 20 == 0) sb.AppendLine();
                }
                if (data.Length > 2048)
                    sb.AppendLine($"\n... (показаны первые 2048 из {data.Length} байт)");

                memoSource.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                txtError.Text = $"Ошибка чтения файла: {ex.Message}";
            }
        }

        // ─── ШИФРОВАНИЕ / ДЕШИФРОВАНИЕ ────────────────────────────────────────────
        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = "";

            if (_selectedFilePath == null)
            {
                txtError.Text = "Файл не выбран!";
                return;
            }

            string validError;
            if (!TryComputeParams(out validError))
            {
                txtError.Text = validError;
                return;
            }

            if (rbEncrypt.IsChecked == true)
            {
                txtR.Text = _r.ToString();
                txtPhi.Text = _phi.ToString();
                txtD.Text = _d.ToString();
            }

            try
            {
                if (rbEncrypt.IsChecked == true)
                    EncryptFile();
                else
                    DecryptFile();
            }
            catch (Exception ex)
            {
                txtError.Text = $"Ошибка: {ex.Message}";
            }
        }

        // ─── ШИФРОВАНИЕ ФАЙЛА ─────────────────────────────────────────────────────
        // Каждый байт mi шифруется: ci = (mi^e) mod r
        // Результат — 16-битное число (т.к. r может быть до ~2^30, но по заданию
        // задание говорит "16-битный блок для 8-битного значения").
        // Фактически сохраняем как uint16 (2 байта на каждый байт входа).
        private void EncryptFile()
        {
            byte[] inputData = File.ReadAllBytes(_selectedFilePath);

            // Проверяем что все байты < r
            if (_r <= 255)
                throw new Exception($"r={_r} должно быть > 255 для шифрования байтов!");

            // Результат: каждый зашифрованный байт - 2 байта (ushort, little-endian)
            byte[] outputData = new byte[inputData.Length * 2];

            var sbResult = new StringBuilder();
            int showCount = Math.Min(inputData.Length, 2048);

            for (int i = 0; i < inputData.Length; i++)
            {
                long mi = inputData[i];
                long ci = FastExp(mi, _e, _r);

                // Сохраняем как 2 байта (ushort little-endian)
                ushort cShort = (ushort)(ci & 0xFFFF);
                outputData[2 * i] = (byte)(cShort & 0xFF);
                outputData[2 * i + 1] = (byte)((cShort >> 8) & 0xFF);

                if (i < showCount)
                {
                    sbResult.Append(ci);
                    if (i < showCount - 1) sbResult.Append(' ');
                    if ((i + 1) % 10 == 0) sbResult.AppendLine();
                }
            }

            if (inputData.Length > 2048)
                sbResult.AppendLine($"\n... (показаны первые 2048 из {inputData.Length} блоков)");

            string outPath = _selectedFilePath + ".rsa";
            File.WriteAllBytes(outPath, outputData);

            memoResult.Text = sbResult.ToString();
            txtStatus.Text = $"✅ Зашифровано! Сохранено: {Path.GetFileName(outPath)}  |  Ko = (e={_e}, r={_r})  |  Kc = (d={_d}, r={_r})";
            txtInfo.Text = $"Файл сохранён: {outPath}";
        }

        // ─── ДЕШИФРОВАНИЕ ФАЙЛА ───────────────────────────────────────────────────
        // Каждые 2 байта (16-битный блок ci) расшифровываются: mi = (ci^d) mod r
        private void DecryptFile()
        {
            byte[] inputData = File.ReadAllBytes(_selectedFilePath);

            if (inputData.Length % 2 != 0)
                throw new Exception("Файл повреждён: длина не кратна 2 (ожидаются 16-битные блоки)");

            int blockCount = inputData.Length / 2;
            byte[] outputData = new byte[blockCount];

            var sbSource = new StringBuilder();
            int showCount = Math.Min(blockCount, 2048);

            var sbResult = new StringBuilder();

            for (int i = 0; i < blockCount; i++)
            {
                // Читаем 2 байта как ushort (little-endian)
                ushort ci = (ushort)(inputData[2 * i] | (inputData[2 * i + 1] << 8));
                long mi = FastExp((long)ci, _d, _r);

                if (mi < 0 || mi > 255)
                    throw new Exception($"Блок {i}: расшифрованное значение {mi} выходит за пределы байта (0..255). Проверьте параметры d и r.");

                outputData[i] = (byte)mi;

                if (i < showCount)
                {
                    sbSource.Append(ci);
                    if (i < showCount - 1) sbSource.Append(' ');
                    if ((i + 1) % 10 == 0) sbSource.AppendLine();

                    sbResult.Append(mi);
                    if (i < showCount - 1) sbResult.Append(' ');
                    if ((i + 1) % 20 == 0) sbResult.AppendLine();
                }
            }

            if (blockCount > 2048)
            {
                sbSource.AppendLine($"\n... (показаны первые 2048 из {blockCount} блоков)");
                sbResult.AppendLine($"\n... (показаны первые 2048 из {blockCount} байт)");
            }

            memoSource.Text = sbSource.ToString();
            memoResult.Text = sbResult.ToString();

            // Сохраняем расшифрованный файл
            // Убираем расширение .rsa если оно есть
            string outPath;
            if (_selectedFilePath.EndsWith(".rsa", StringComparison.OrdinalIgnoreCase))
                outPath = _selectedFilePath.Substring(0, _selectedFilePath.Length - 4);
            else
                outPath = _selectedFilePath;

            File.WriteAllBytes(outPath, outputData);
            txtStatus.Text = $"✅ Расшифровано! Сохранено: {Path.GetFileName(outPath)}  |  Kc = (d={_d}, r={_r})";
            txtInfo.Text = $"Файл сохранён: {outPath}";
        }
    }
}
