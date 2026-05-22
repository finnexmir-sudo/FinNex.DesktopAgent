using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        private readonly string? _url;
        private readonly string  _baseUrl;
        private readonly Panel   _canvas;
        private Rectangle _closeHit;
        private Rectangle _goHit;

        private static readonly Color BgColor    = Color.FromArgb(50, 50, 54);
        private static readonly Color TitleColor = Color.White;
        private static readonly Color MetnColor  = Color.FromArgb(200, 200, 200);
        private static readonly Color BlueColor  = Color.FromArgb(100, 180, 255);

        internal NotificationPopup(string bashliq, string metn, string? url, string baseUrl)
        {
            _url     = url;
            _baseUrl = baseUrl;

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = BgColor;
            Width           = 340;

            // Panel bütün forma doldurur — öz Paint-ini işlədir
            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
            _canvas.Paint      += OnCanvasPaint;
            _canvas.MouseClick += OnCanvasClick;
            _canvas.MouseMove  += OnCanvasMove;
            Controls.Add(_canvas);

            // Hündürlüyü mətnə görə hesabla
            const int pad = 12;
            bool hasUrl   = !string.IsNullOrEmpty(url);

            using var tf = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var mf = new Font("Segoe UI",  8.5f);

            int titleH = TextRenderer.MeasureText(
                bashliq, tf, new Size(Width - pad * 2 - 30, 0),
                TextFormatFlags.WordBreak).Height + 4;

            int metnH = TextRenderer.MeasureText(
                metn, mf, new Size(Width - pad * 2, 0),
                TextFormatFlags.WordBreak).Height + 4;

            int formH = pad + titleH + 6 + metnH + pad;
            if (hasUrl) formH += 34;
            formH = Math.Max(formH, 80);
            Height = formH;

            _closeHit = new Rectangle(Width - 32, 4, 26, 26);
            if (hasUrl)
                _goHit = new Rectangle(Width - 120, formH - 32, 108, 24);

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            // Forma tam göründükdən sonra məcburi repaint
            Shown += (_, _) => { _canvas.Invalidate(); _canvas.Update(); };
        }

        private void OnCanvasPaint(object? s, PaintEventArgs e)
        {
            var g   = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BgColor);

            const int pad = 12;
            int w         = Width;
            bool hasUrl   = !string.IsNullOrEmpty(_url);

            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var metnFont  = new Font("Segoe UI",  8.5f);
            using var smallFont = new Font("Segoe UI",  8.5f);

            // Bağlama
            TextRenderer.DrawText(g, "✕", smallFont, _closeHit, MetnColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Başlıq
            var titleRect = new Rectangle(pad, pad, w - pad * 2 - 34, 120);
            TextRenderer.DrawText(g, _bashliq(), titleFont, titleRect, TitleColor,
                TextFormatFlags.WordBreak | TextFormatFlags.Top);

            int titleH = TextRenderer.MeasureText(
                _bashliq(), titleFont, new Size(titleRect.Width, 0),
                TextFormatFlags.WordBreak).Height;

            // Mətn
            var metnRect = new Rectangle(pad, pad + titleH + 6, w - pad * 2, 200);
            TextRenderer.DrawText(g, _metn(), metnFont, metnRect, MetnColor,
                TextFormatFlags.WordBreak | TextFormatFlags.Top);

            // Keçid düyməsi
            if (hasUrl)
            {
                using var btnBg  = new SolidBrush(Color.FromArgb(62, 62, 66));
                using var border = new Pen(Color.FromArgb(90, 90, 95));
                g.FillRectangle(btnBg, _goHit);
                g.DrawRectangle(border, _goHit);
                TextRenderer.DrawText(g, "→  Keçid Et", smallFont, _goHit,
                    BlueColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // _bashliq və _metn-i lazim olan string-lər
        private string _bashliqVal = "";
        private string _metnVal    = "";
        private string _bashliq()  => _bashliqVal;
        private string _metn()     => _metnVal;

        // Constructor-dan sonra dəyərləri təyin etmək üçün factory
        internal static NotificationPopup Create(
            string bashliq, string metn, string? url, string baseUrl)
        {
            var p = new NotificationPopup(bashliq, metn, url, baseUrl);
            p._bashliqVal = bashliq;
            p._metnVal    = metn;
            return p;
        }

        private void OnCanvasClick(object? s, MouseEventArgs e)
        {
            if (_closeHit.Contains(e.Location)) { Close(); return; }
            if (!string.IsNullOrEmpty(_url))
            {
                if (_goHit.Contains(e.Location) || true) Navigate();
            }
        }

        private void OnCanvasMove(object? s, MouseEventArgs e)
        {
            _canvas.Cursor = Cursors.Hand;
        }

        private void Navigate()
        {
            if (string.IsNullOrEmpty(_url)) return;
            Process.Start(new ProcessStartInfo(
                _baseUrl.TrimEnd('/') + _url) { UseShellExecute = true });
            Close();
        }
    }
}
