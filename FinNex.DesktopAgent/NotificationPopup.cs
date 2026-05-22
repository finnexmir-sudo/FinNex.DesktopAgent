using System;
using System.Drawing;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    internal class NotificationPopup : Form
    {
        internal NotificationPopup(string bashliq, string metn)
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost          = true;
            ShowInTaskbar    = false;
            StartPosition    = FormStartPosition.Manual;
            BackColor        = Color.FromArgb(45, 45, 48);
            Width            = 340;

            const int pad     = 14;
            const int iconSz  = 28;
            int contentX      = pad + iconSz + 10;
            int contentW      = Width - contentX - 40;

            var picIcon = new PictureBox
            {
                Image    = SystemIcons.Information.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size     = new Size(iconSz, iconSz),
                Location = new Point(pad, pad + 2),
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
            btnClose.FlatAppearance.BorderSize          = 0;
            btnClose.FlatAppearance.MouseOverBackColor  = Color.FromArgb(75, 75, 78);
            btnClose.Click += (_, _) => Close();

            Height = Math.Max(picIcon.Bottom, lblMetn.Bottom) + pad;

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 8);

            Controls.AddRange(new Control[] { picIcon, lblTitle, lblMetn, btnClose });

            // Popup-a klikləndə də bağlansin
            foreach (Control c in new Control[] { lblTitle, lblMetn })
                c.Click += (_, _) => Close();
        }
    }
}
