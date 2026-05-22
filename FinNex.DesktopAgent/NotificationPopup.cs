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

        internal NotificationPopup(string bashliq, string metn, string? url, string baseUrl)
        {
            _url     = url;
            _baseUrl = baseUrl;

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = Color.FromArgb(45, 45, 48);
            Width           = 340;

            const int pad    = 14;
            const int iconSz = 28;
            int contentX     = pad + iconSz + 10;
            int contentW     = Width - contentX - 40;

            var picIcon = new PictureBox
            {
                Image     = SystemIcons.Information.ToBitmap(),
                SizeMode  = PictureBoxSizeMode.StretchImage,
                Size      = new Size(iconSz, iconSz),
                Location  = new Point(pad, pad + 2),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text      = bashliq,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location  = new Point(contentX, pad),
                Size      = new Size(contentW, 0),
                AutoSize  = false
            };
            lblTitle.Height = TextRenderer.MeasureText(
                bashliq, lblTitle.Font,
                new Size(contentW, int.MaxValue),
                TextFormatFlags.WordBreak).Height + 2;

            var lblMetn = new Label
            {
                Text      = metn,
                ForeColor = Color.FromArgb(190, 190, 190),
                Font      = new Font("Segoe UI", 8.5f),
                Location  = new Point(contentX, pad + lblTitle.Height + 4),
                Size      = new Size(contentW, 0),
                AutoSize  = false
            };
            lblMetn.Height = TextRenderer.MeasureText(
                metn, lblMetn.Font,
                new Size(contentW, int.MaxValue),
                TextFormatFlags.WordBreak).Height + 2;

            var btnClose = new Button
            {
                Text      = "✕",
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(26, 26),
                Location  = new Point(Width - 32, 6),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnClose.FlatAppearance.BorderSize         = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 78);
            btnClose.Click += (_, _) => Close();

            int bodyBottom = Math.Max(picIcon.Bottom, lblMetn.Bottom) + pad;

            var controls = new List<Control> { picIcon, lblTitle, lblMetn, btnClose };

            if (!string.IsNullOrEmpty(url))
            {
                var btnGo = new Button
                {
                    Text      = "→ Keçid Et",
                    ForeColor = Color.FromArgb(100, 180, 255),
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Size      = new Size(90, 22),
                    Location  = new Point(contentX, bodyBottom),
                    Cursor    = Cursors.Hand,
                    TabStop   = false
                };
                btnGo.FlatAppearance.BorderSize         = 0;
                btnGo.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 63);
                btnGo.Click += (_, _) => Navigate();
                controls.Add(btnGo);

                bodyBottom += 30;

                // Başlıq və mətnə klikləmək də keçid edir
                lblTitle.Cursor = Cursors.Hand;
                lblMetn.Cursor  = Cursors.Hand;
                lblTitle.Click += (_, _) => Navigate();
                lblMetn.Click  += (_, _) => Navigate();
            }

            Height = bodyBottom;

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            Controls.AddRange(controls.ToArray());
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
