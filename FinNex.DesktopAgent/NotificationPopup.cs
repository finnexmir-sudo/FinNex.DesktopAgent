using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    /// <summary>
    /// Müasir dizaynlı masaüstü bildiriş popup-u.
    /// Sağ alt küncdən yuxarı sürüşərək çıxır, üst-üstə düzülür.
    /// Hər BildirisNovu üçün fərqli rəng mövzusu var.
    /// </summary>
    internal sealed class NotificationPopup : Form
    {
        // ── Layout sabitləri ──────────────────────────────────────────
        private const int W          = 360;
        private const int H          = 115;
        private const int Gap        = 8;
        private const int Edge       = 14;
        private const int Stripe     = 5;
        private const int MaxVisible = 5;   // eyni anda ən çox 5 popup

        // ── Aktiv popup stack-i (hamisı UI thread-də — lock lazım deyil) ────
        private static readonly List<NotificationPopup> _stack = new();

        // ── Məlumat ───────────────────────────────────────────────────
        private readonly Color   _bg;
        private readonly Color   _accent;
        private readonly string  _icon;
        private readonly string  _bashliq;
        private readonly string  _metn;
        private readonly string? _url;
        private readonly string  _baseUrl;
        private readonly string  _timeStr = DateTime.Now.ToString("HH:mm");

        // ── Timerlər ─────────────────────────────────────────────────
        private System.Windows.Forms.Timer? _slideTimer;
        private System.Windows.Forms.Timer? _autoTimer;
        private int _targetY;

        // ── × düyməsi hit rect (sağ üst künc — saatdan ayrı) ────────────
        private static readonly Rectangle CloseBox = new(W - 28, 7, 22, 22);

        // ── Constructor ────────────────────────────────────────────────
        internal NotificationPopup(
            string bashliq, string metn, string? url, string baseUrl,
            int nov = 0, int autoCloseMs = 0)
        {
            (_bashliq, _metn, _url, _baseUrl) = (bashliq, metn, url, baseUrl);
            (_bg, _accent, _icon) = PickTheme(nov);

            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(W, H);
            ShowInTaskbar   = false;
            TopMost         = true;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = _bg;
            DoubleBuffered  = true;

            // Yuvarlaq künclər
            var gp = new GraphicsPath();
            const int cr = 9;
            gp.AddArc(0,      0,      cr * 2, cr * 2, 180, 90);
            gp.AddArc(W-cr*2, 0,      cr * 2, cr * 2, 270, 90);
            gp.AddArc(W-cr*2, H-cr*2, cr * 2, cr * 2, 0,   90);
            gp.AddArc(0,      H-cr*2, cr * 2, cr * 2, 90,  90);
            gp.CloseFigure();
            Region = new Region(gp);
            gp.Dispose();

            // Eyni anda 5-dən çox popup olmasın — limit aşılarsa ən köhnəni bağla
            while (_stack.Count >= MaxVisible)
                _stack[0].SafeClose();

            // Mövqe: aşağıda başla, stack sayına görə hədəf hesabla
            var wa    = Screen.PrimaryScreen!.WorkingArea;
            var idx   = _stack.Count;
            _targetY  = wa.Bottom - H - Edge - idx * (H + Gap);
            Location  = new Point(wa.Right - W - Edge, wa.Bottom + 10);

            _stack.Add(this);

            Paint     += OnPaint;
            MouseDown += OnMouseDown;

            if (autoCloseMs > 0)
            {
                _autoTimer = new System.Windows.Forms.Timer { Interval = autoCloseMs };
                _autoTimer.Tick += (_, _) => SafeClose();
                _autoTimer.Start();
            }
        }

        // ── Show ─────────────────────────────────────────────────────
        public new void Show()
        {
            base.Show();
            SlideTo(_targetY);
        }

        // ── Rəng mövzuları (BildirisNovu dəyərlərinə görə) ─────────────────
        private static (Color bg, Color accent, string icon) PickTheme(int nov) => nov switch
        {
            // 💬 Mesaj
            7
                => (C(22, 48, 56),  C(0,   210, 175), "\U0001F4AC"),
            // 📋 Tapşırıq
            12 or 13 or 14 or 15
                => (C(25, 38, 65),  C(80,  160, 255), "\U0001F4CB"),
            // 🏖 Məzuniyyət / İcazə
            1 or 2 or 3 or 4 or 5 or 6 or 8 or 9 or 10 or 30 or 31
                => (C(58, 38, 18),  C(255, 150,  60), "\U0001F3D6"),
            // 💰 Maaş
            11 or 27 or 28 or 29
                => (C(50, 42, 15),  C(220, 185,  60), "\U0001F4B0"),
            // 💵 Avans
            20 or 21 or 22
                => (C(20, 52, 38),  C(80,  205, 155), "\U0001F4B5"),
            // 🧾 Xərc
            23 or 24 or 25 or 26
                => (C(45, 32, 55),  C(175, 130, 255), "\U0001F9FE"),
            // ⭐ Jeton
            32 or 34 or 35
                => (C(50, 46, 15),  C(240, 200,  60), "⭐"),
            // ⛔ Qara Jeton
            33
                => (C(55, 20, 20),  C(255,  80,  80), "⛔"),
            // 👥 Görüş
            16 or 17 or 18
                => (C(55, 25, 38),  C(255, 100, 140), "\U0001F465"),
            // 📄 Müqavilə
            19
                => (C(28, 45, 52),  C(80,  190, 200), "\U0001F4C4"),
            // 💡 Teklif
            36 or 37
                => (C(22, 46, 42),  C(60,  200, 170), "\U0001F4A1"),
            // 📧 Mail
            38
                => (C(30, 28, 62),  C(130, 120, 255), "\U0001F4E7"),
            // 📊 Performans
            39 or 40 or 41 or 42
                => (C(40, 25, 60),  C(175, 120, 255), "\U0001F4CA"),
            // ✅ İş günü bitdi
            43
                => (C(30, 62, 43),  C(120, 220, 150), "✅"),
            // 🔔 Default
            _
                => (C(50, 50, 54),  C(100, 180, 255), "\U0001F514"),
        };

        private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

        // ── Çəkmə ──────────────────────────────────────────────────────
        private void OnPaint(object? _, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Sol aksent zolağı
            using var ab = new SolidBrush(_accent);
            g.FillRectangle(ab, 0, 0, Stripe, H);

            // Emoji ikon
            using var iconFont = new Font("Segoe UI Emoji", 13f);
            g.DrawString(_icon, iconFont, ab,
                new RectangleF(Stripe + 8, 10, 26, 26), sf);

            // Başlıq (saat və × düyməsinə qədər məhdud)
            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var white     = new SolidBrush(Color.White);
            g.DrawString(_bashliq, titleFont, white,
                new RectangleF(Stripe + 40, 11, W - Stripe - 160, 24),
                new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming      = StringTrimming.EllipsisCharacter
                });

            // Saat (sağ üst — × düyməsindən solda, üst-üstə düşməsin)
            using var dim      = new SolidBrush(Color.FromArgb(120, Color.White));
            using var timeFont = new Font("Segoe UI", 7.5f);
            g.DrawString(_timeStr, timeFont, dim,
                new RectangleF(W - 112, 13, 76, 16),
                new StringFormat
                {
                    Alignment     = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                });

            // Ayırıcı xətt
            using var divPen = new Pen(Color.FromArgb(48, _accent));
            g.DrawLine(divPen, Stripe + 8, 40, W - 12, 40);

            // Mətn gövdəsi
            using var bodyFont  = new Font("Segoe UI", 8.8f);
            using var bodyBrush = new SolidBrush(Color.FromArgb(200, Color.White));
            g.DrawString(_metn, bodyFont, bodyBrush,
                new RectangleF(Stripe + 10, 47, W - Stripe - 20, H - 56),
                new StringFormat
                {
                    Trimming    = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.LineLimit
                });

            // × bağla düyməsi (sağ üst künc — daha parlaq)
            using var closeBrush = new SolidBrush(Color.FromArgb(175, Color.White));
            using var closeFont  = new Font("Segoe UI", 13f);
            g.DrawString("×", closeFont, closeBrush, CloseBox, sf);

            sf.Dispose();
        }

        // ── Siçan ───────────────────────────────────────────────────────
        private void OnMouseDown(object? _, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (CloseBox.Contains(e.Location)) { SafeClose(); return; }

            if (_url != null)
            {
                var href = _url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? _url
                    : $"{_baseUrl.TrimEnd('/')}{_url}";
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(href)
                        { UseShellExecute = true });
                }
                catch { }
            }
            SafeClose();
        }

        // ── Sürüş animasiyası ────────────────────────────────────────
        private void SlideTo(int targetY)
        {
            _targetY = targetY;
            _slideTimer?.Stop();
            _slideTimer?.Dispose();
            _slideTimer = new System.Windows.Forms.Timer { Interval = 12 };
            _slideTimer.Tick += (s, _) =>
            {
                var t = (System.Windows.Forms.Timer)s!;
                if (IsDisposed) { t.Stop(); return; }
                var dist = Top - _targetY;
                if (dist <= 0) { Top = _targetY; t.Stop(); return; }
                Top -= Math.Max(1, dist / 4);
            };
            _slideTimer.Start();
        }

        // ── Bağla ────────────────────────────────────────────────────────
        internal void SafeClose()
        {
            _autoTimer?.Stop();
            _slideTimer?.Stop();
            _stack.Remove(this);
            Restack();
            Close();   // FormClosed → TrayAgent sayacı azaldır
        }

        // ── Qalanları yenidən yerləşdir ───────────────────────────────
        private static void Restack()
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            for (var i = 0; i < _stack.Count; i++)
            {
                var p = _stack[i];
                if (!p.IsDisposed)
                    p.SlideTo(wa.Bottom - H - Edge - i * (H + Gap));
            }
        }

        // ── Dispose ─────────────────────────────────────────────────────
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _slideTimer?.Dispose();
                _autoTimer?.Dispose();
                _stack.Remove(this);
            }
            base.Dispose(disposing);
        }

        // ── Fokus oğurlamasın ───────────────────────────────────────
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
