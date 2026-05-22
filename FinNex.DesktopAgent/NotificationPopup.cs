using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        // Server-dəki BildirisNovu.IsGunuBitdi enum dəyəri.
        // Bu növ bildiriş digərlərindən fərqli (yaşıl) rəngdə göstərilir.
        private const int NovIsGunuBitdi = 43;

        private readonly string  _bashliq;
        private readonly string  _metn;
        private readonly string? _url;
        private readonly string  _baseUrl;
        private readonly Panel   _canvas;
        private readonly Color   _bgColor;
        private readonly Color   _accentColor;
        private Rectangle _closeHit;
        private Rectangle _goHit;
        private float     _progressRatio = -1f;

        private static readonly Color TitleColor = Color.White;
        private static readonly Color MetnColor  = Color.FromArgb(205, 205, 205);
        private static readonly Color DimColor   = Color.FromArgb(90, 90, 95);

        internal NotificationPopup(
            string bashliq, string metn, string? url, string baseUrl,
            int nov = 0, int autoCloseMs = 0)
        {
            _bashliq = bashliq;
            _metn    = metn;
            _url     = url;
            _baseUrl = baseUrl;

            // İş günü bitdi bildirişi — yaşıl fon; digərləri — tünd boz fon.
            if (nov == NovIsGunuBitdi)
            {
                _bgColor     = Color.FromArgb(30, 62, 43);
                _accentColor = Color.FromArgb(120, 220, 150);
            }
            else
            {
                _bgColor     = Color.FromArgb(50, 50, 54);
                _accentColor = Color.FromArgb(100, 180, 255);
            }

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = _bgColor;
            Cursor          = Cursors.Default;
            Width           = 340;

            _canvas = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = _bgColor,
                Cursor    = Cursors.Default
            };
            _canvas.Paint      += OnCanvasPaint;
            _canvas.MouseClick += OnCanvasClick;
            Controls.Add(_canvas);

            const int pad   = 12;
            bool      hasUrl = !string.IsNullOrEmpty(url);

            using var tf = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var mf = new Font("Segoe UI",  8.5f);

            int titleH = TextRenderer.MeasureText(
                bashliq, tf, new Size(Width - pad * 2 - 30, 0),
                TextFormatFlags.WordBreak).Height + 4;
            int metnH = TextRenderer.MeasureText(
                metn, mf, new Size(Width - pad * 2, 0),
                TextFormatFlags.WordBreak).Height + 4;

            int formH = pad + titleH + 6 + metnH + pad;
            if (hasUrl)          formH += 34;
            if (autoCloseMs > 0) formH +=  6;
            formH  = Math.Max(formH, 80);
            Height = formH;

            _closeHit = new Rectangle(Width - 32, 4, 26, 26);
            if (hasUrl)
                _goHit = new Rectangle(
                    Width - 120,
                    formH - (autoCloseMs > 0 ? 38 : 34),
                    108, 24);

            var wa = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            Shown += (_, _) => _canvas.Invalidate();

            if (autoCloseMs > 0)
            {
                _progressRatio = 1f;
                System.Threading.Timer? t = null;
                t = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke(new Action(() => { if (!IsDisposed) Close(); }));
                    }
                    catch { }
                    finally { t?.Dispose(); }
                }, null, autoCloseMs, System.Threading.Timeout.Infinite);
                FormClosed += (_, __) => { try { t?.Dispose(); } catch { } };
            }
        }

        private void OnCanvasPaint(object? s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(_bgColor);

            const int pad = 12;
            int       w   = Width;
            bool hasUrl   = !string.IsNullOrEmpty(_url);

            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var metnFont  = new Font("Segoe UI",  8.5f);

            // X düyməsi
            var xColor = _closeHit.Contains(_canvas.PointToClient(MousePosition))
                ? Color.White : MetnColor;
            TextRenderer.DrawText(g, "✕", metnFont, _closeHit, xColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Başlıq
            var titleRect = new Rectangle(pad, pad, w - pad * 2 - 34, 120);
            TextRenderer.DrawText(g, _bashliq, titleFont, titleRect, TitleColor,
                TextFormatFlags.WordBreak | TextFormatFlags.Top);

            int titleH = TextRenderer.MeasureText(
                _bashliq, titleFont,
                new Size(titleRect.Width, 0), TextFormatFlags.WordBreak).Height;

            // Mətn
            var metnRect = new Rectangle(pad, pad + titleH + 6, w - pad * 2, 200);
            TextRenderer.DrawText(g, _metn, metnFont, metnRect, MetnColor,
                TextFormatFlags.WordBreak | TextFormatFlags.Top);

            // Keçid düyməsi
            if (hasUrl)
            {
                bool hover = _goHit.Contains(_canvas.PointToClient(MousePosition));
                using var btnBg  = new SolidBrush(hover
                    ? Color.FromArgb(75, 75, 80)
                    : Color.FromArgb(62, 62, 66));
                using var border = new Pen(Color.FromArgb(90, 90, 95));
                g.FillRectangle(btnBg, _goHit);
                g.DrawRectangle(border, _goHit);
                TextRenderer.DrawText(g, "→  Keçid Et", metnFont, _goHit,
                    _accentColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Progress bar (auto-close üçün)
            if (_progressRatio >= 0f)
            {
                int barW = (int)(w * _progressRatio);
                using var barBrush   = new SolidBrush(_accentColor);
                using var trackBrush = new SolidBrush(DimColor);
                if (barW > 0)  g.FillRectangle(barBrush,   0,    Height - 5, barW,     5);
                if (barW < w)  g.FillRectangle(trackBrush, barW, Height - 5, w - barW, 5);
            }
        }

        private void OnCanvasClick(object? s, MouseEventArgs e)
        {
            if (_closeHit.Contains(e.Location))
            {
                // X düyməsi: popup-u bağla
                Close();
                return;
            }

            if (!string.IsNullOrEmpty(_url) && _goHit.Contains(e.Location))
            {
                // Keçid Et düyməsi: brauzerdə aç və bağla
                Navigate();
                return;
            }

            // Digər ərazələr: heç bir şey etmə (popup qalsın)
        }

        private void Navigate()
        {
            if (string.IsNullOrEmpty(_url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(
                    _baseUrl.TrimEnd('/') + _url) { UseShellExecute = true });
            }
            catch { }
            Close();
        }
    }
}
