using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        private readonly string  _bashliq;
        private readonly string  _metn;
        private readonly string? _url;
        private readonly string  _baseUrl;
        private readonly Panel   _canvas;
        private Rectangle _closeHit;
        private Rectangle _goHit;
        private float     _progressRatio = -1f;

        private static readonly Color BgColor    = Color.FromArgb(50, 50, 54);
        private static readonly Color TitleColor = Color.White;
        private static readonly Color MetnColor  = Color.FromArgb(200, 200, 200);
        private static readonly Color BlueColor  = Color.FromArgb(100, 180, 255);
        private static readonly Color DimColor   = Color.FromArgb(90, 90, 95);

        /// <param name="autoCloseMs">0 = qalir; >0 = ms sonra avtomatik bağlanır</param>
        internal NotificationPopup(
            string bashliq, string metn, string? url, string baseUrl,
            int autoCloseMs = 0)
        {
            _bashliq = bashliq;
            _metn    = metn;
            _url     = url;
            _baseUrl = baseUrl;

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = BgColor;
            Cursor          = Cursors.Hand;
            Width           = 340;

            _canvas = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = BgColor,
                Cursor    = Cursors.Hand
            };
            _canvas.Paint      += OnCanvasPaint;
            _canvas.MouseClick += OnCanvasClick;
            Controls.Add(_canvas);

            const int pad = 12;
            bool hasUrl   = !string.IsNullOrEmpty(url);

            using var tf = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var mf = new Font("Segoe UI",  8.5f);

            int titleH = TextRenderer.MeasureText(
                bashliq, tf, new Size(Width - pad * 2 - 30, 0),
                TextFormatFlags.WordBreak).Height + 4;
            int metnH  = TextRenderer.MeasureText(
                metn, mf, new Size(Width - pad * 2, 0),
                TextFormatFlags.WordBreak).Height + 4;

            int formH = pad + titleH + 6 + metnH + pad;
            if (hasUrl)          formH += 34;
            if (autoCloseMs > 0) formH +=  4;
            formH  = Math.Max(formH, 80);
            Height = formH;

            _closeHit = new Rectangle(Width - 32, 4, 26, 26);
            if (hasUrl)
                _goHit = new Rectangle(
                    Width - 120,
                    formH - (autoCloseMs > 0 ? 36 : 32),
                    108, 24);

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            Shown += (_, _) => { _canvas.Invalidate(); _canvas.Update(); };

            if (autoCloseMs > 0)
            {
                const int steps    = 40;
                int       interval = Math.Max(autoCloseMs / steps, 30);
                int       step     = 0;
                _progressRatio     = 1f;

                // Tam ad: System.Windows.Forms.Timer
                var timer = new System.Windows.Forms.Timer { Interval = interval };
                timer.Tick += (_, _) =>
                {
                    step++;
                    _progressRatio = 1f - (float)step / steps;
                    _canvas.Invalidate();
                    if (step >= steps) { timer.Stop(); timer.Dispose(); Close(); }
                };
                Shown += (_, _) => timer.Start();
            }
        }

        private void OnCanvasPaint(object? s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BgColor);

            const int pad = 12;
            int w         = Width;
            bool hasUrl   = !string.IsNullOrEmpty(_url);

            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var metnFont  = new Font("Segoe UI",  8.5f);

            // Bağlama düyməsi
            TextRenderer.DrawText(g, "✕", metnFont, _closeHit, MetnColor,
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
                using var btnBg  = new SolidBrush(Color.FromArgb(62, 62, 66));
                using var border = new Pen(Color.FromArgb(90, 90, 95));
                g.FillRectangle(btnBg, _goHit);
                g.DrawRectangle(border, _goHit);
                TextRenderer.DrawText(g, "→  Keçid Et", metnFont, _goHit,
                    BlueColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Progress bar
            if (_progressRatio >= 0f)
            {
                int barW = (int)(w * _progressRatio);
                using var barBrush   = new SolidBrush(Color.FromArgb(80, 120, 200));
                using var trackBrush = new SolidBrush(DimColor);
                if (barW > 0)  g.FillRectangle(barBrush,   0,    Height - 4, barW,     4);
                if (barW < w)  g.FillRectangle(trackBrush, barW, Height - 4, w - barW, 4);
            }
        }

        private void OnCanvasClick(object? s, MouseEventArgs e)
        {
            if (_closeHit.Contains(e.Location)) { Close(); return; }
            if (!string.IsNullOrEmpty(_url)) Navigate();
            else Close();
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
