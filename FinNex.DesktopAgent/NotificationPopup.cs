using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        private readonly string _bashliq;
        private readonly string _metn;
        private readonly string? _url;
        private readonly string _baseUrl;

        private static readonly Color   BgColor    = Color.FromArgb(45, 45, 48);
        private static readonly Color   TitleColor = Color.White;
        private static readonly Color   MetnColor  = Color.FromArgb(190, 190, 190);
        private static readonly Color   BlueColor  = Color.FromArgb(100, 180, 255);
        private static readonly Font    TitleFont  = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font    MetnFont   = new Font("Segoe UI",  8.5f);
        private static readonly Font    SmallFont  = new Font("Segoe UI",  8.5f);
        private static readonly Brush   TitleBrush = new SolidBrush(TitleColor);
        private static readonly Brush   MetnBrush  = new SolidBrush(MetnColor);
        private static readonly Brush   BlueBrush  = new SolidBrush(BlueColor);

        private Rectangle _closeHit;
        private Rectangle _goHit;

        internal NotificationPopup(string bashliq, string metn, string? url, string baseUrl)
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
            DoubleBuffered  = true;
            Width           = 340;

            const int pad  = 12;
            const int w    = 340;

            // Başlıq yüksəkliyi
            var titleSz = TextRenderer.MeasureText(
                bashliq, TitleFont, new Size(w - pad * 2 - 30, 0),
                TextFormatFlags.WordBreak);

            // Mətn yüksəkliyi
            var metnSz = TextRenderer.MeasureText(
                metn, MetnFont, new Size(w - pad * 2, 0),
                TextFormatFlags.WordBreak);

            int titleH  = titleSz.Height + 4;
            int metnH   = metnSz.Height  + 4;
            int bodyBot = pad + titleH + 6 + metnH + pad;

            bool hasUrl = !string.IsNullOrEmpty(url);
            int  formH  = hasUrl ? bodyBot + 34 : bodyBot;
            formH = Math.Max(formH, 75);

            Height = formH;

            _closeHit = new Rectangle(w - 32, 4, 26, 26);
            if (hasUrl)
                _goHit = new Rectangle(w - 118, formH - 32, 106, 24);

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            MouseClick += HandleClick;
            MouseMove  += HandleMove;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            const int pad = 12;
            int w = Width;

            // Arxa fon (DoubleBuffer olsa da eəmin olmaq üçün)
            g.Clear(BgColor);

            // ─ Bağlama düyməsi
            g.DrawString("✕", SmallFont, MetnBrush, _closeHit,
                new StringFormat { Alignment = StringAlignment.Center,
                                   LineAlignment = StringAlignment.Center });

            // ─ Başlıq
            g.DrawString(_bashliq, TitleFont, TitleBrush,
                new RectangleF(pad, pad, w - pad * 2 - 32, 120));

            var titleSz = TextRenderer.MeasureText(
                _bashliq, TitleFont, new Size(w - pad * 2 - 30, 0),
                TextFormatFlags.WordBreak);
            int metnY = pad + titleSz.Height + 6;

            // ─ Mətn
            g.DrawString(_metn, MetnFont, MetnBrush,
                new RectangleF(pad, metnY, w - pad * 2, 200));

            // ─ "Keçid Et" düyməsi (URL varsa)
            if (!string.IsNullOrEmpty(_url))
            {
                using var btnBg  = new SolidBrush(Color.FromArgb(58, 58, 62));
                using var border = new Pen(Color.FromArgb(85, 85, 90));
                g.FillRectangle(btnBg, _goHit);
                g.DrawRectangle(border, _goHit);
                g.DrawString("→  Keçid Et", SmallFont, BlueBrush, _goHit,
                    new StringFormat { Alignment     = StringAlignment.Center,
                                       LineAlignment = StringAlignment.Center });
            }
        }

        private void HandleClick(object? s, MouseEventArgs e)
        {
            if (_closeHit.Contains(e.Location))
            {
                Close();
            }
            else if (!string.IsNullOrEmpty(_url) && _goHit.Contains(e.Location))
            {
                Navigate();
            }
            else if (!string.IsNullOrEmpty(_url))
            {
                Navigate(); // popup-un haraından kliklensə keçid edir
            }
        }

        private void HandleMove(object? s, MouseEventArgs e)
        {
            bool onClose = _closeHit.Contains(e.Location);
            bool onGo    = !string.IsNullOrEmpty(_url) && _goHit.Contains(e.Location);
            Cursor = (onClose || onGo || !string.IsNullOrEmpty(_url))
                ? Cursors.Hand : Cursors.Default;
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
