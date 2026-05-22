using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        private readonly string? _url;
        private readonly string _baseUrl;
        private static readonly Color BgColor   = Color.FromArgb(45, 45, 48);
        private static readonly Color TextColor = Color.FromArgb(230, 230, 230);
        private static readonly Color DimColor  = Color.FromArgb(170, 170, 170);

        internal NotificationPopup(string bashliq, string metn, string? url, string baseUrl)
        {
            _url     = url;
            _baseUrl = baseUrl;

            SuspendLayout();

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = BgColor;
            Width           = 340;
            Font            = new Font("Segoe UI", 9f);

            // ── Bağlama düyməsi ──
            var btnClose = new Button
            {
                Text      = "✕",
                ForeColor = DimColor,
                BackColor = BgColor,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(28, 28),
                Location  = new Point(Width - 34, 4),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnClose.FlatAppearance.BorderSize         = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 73);
            btnClose.Click += (_, _) => Close();

            // ── İkona ──
            var picIcon = new PictureBox
            {
                Image     = SystemIcons.Information.ToBitmap(),
                SizeMode  = PictureBoxSizeMode.StretchImage,
                Size      = new Size(26, 26),
                Location  = new Point(12, 14),
                BackColor = BgColor
            };

            // ── Başlıq ──
            var lblTitle = new Label
            {
                Text      = bashliq,
                ForeColor = Color.White,
                BackColor = BgColor,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location  = new Point(46, 12),
                Size      = new Size(Width - 46 - 38, 20),
                AutoSize  = false
            };

            // ── Mətn ──
            var lblMetn = new Label
            {
                Text      = metn,
                ForeColor = DimColor,
                BackColor = BgColor,
                Font      = new Font("Segoe UI", 8.5f),
                Location  = new Point(12, 40),
                Size      = new Size(Width - 24, 0),
                AutoSize  = false
            };
            lblMetn.Height = TextRenderer.MeasureText(
                metn, lblMetn.Font,
                new Size(lblMetn.Width, int.MaxValue),
                TextFormatFlags.WordBreak).Height + 4;

            int nextY = lblMetn.Bottom + 10;
            var controls = new List<Control> { btnClose, picIcon, lblTitle, lblMetn };

            // ── Keçid düyməsi (yalnız URL varsa) ──
            if (!string.IsNullOrEmpty(url))
            {
                var btnGo = new Button
                {
                    Text      = "→  Keçid Et",
                    ForeColor = Color.FromArgb(100, 180, 255),
                    BackColor = Color.FromArgb(55, 55, 58),
                    FlatStyle = FlatStyle.Flat,
                    Size      = new Size(100, 26),
                    Location  = new Point(Width - 116, nextY - 2),
                    Cursor    = Cursors.Hand,
                    TabStop   = false
                };
                btnGo.FlatAppearance.BorderSize         = 1;
                btnGo.FlatAppearance.BorderColor        = Color.FromArgb(80, 80, 83);
                btnGo.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 68);
                btnGo.Click += (_, _) => Navigate();
                controls.Add(btnGo);

                nextY = btnGo.Bottom + 10;

                lblTitle.Cursor = Cursors.Hand;
                lblMetn.Cursor  = Cursors.Hand;
                lblTitle.Click += (_, _) => Navigate();
                lblMetn.Click  += (_, _) => Navigate();
            }

            Height = Math.Max(nextY, 70);

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            Controls.AddRange(controls.ToArray());
            ResumeLayout(true);
        }

        private void Navigate()
        {
            if (string.IsNullOrEmpty(_url)) return;
            var fullUrl = _baseUrl.TrimEnd('/') + _url;
            Process.Start(new ProcessStartInfo(fullUrl) { UseShellExecute = true });
            Close();
        }
    }
}
