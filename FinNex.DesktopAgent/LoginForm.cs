using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FinNex.DesktopAgent
{
    public class LoginForm : Form
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblError;
        private readonly AppConfig _config;

        public string Token   { get; private set; }
        public string IsciAd  { get; private set; }
        public int    IsciId  { get; private set; }

        public LoginForm(AppConfig config)
        {
            _config = config;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "FinNex Desktop Agent - Giriş";
            this.Size = new Size(350, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblUser = new Label { Text = "İstifadəçi adı:", Location = new System.Drawing.Point(20, 25), Size = new Size(90, 20) };
            txtUsername = new TextBox { Location = new System.Drawing.Point(120, 22), Size = new Size(180, 20) };

            Label lblPass = new Label { Text = "Şifrə:", Location = new System.Drawing.Point(20, 65), Size = new Size(90, 20) };
            txtPassword = new TextBox { Location = new System.Drawing.Point(120, 62), Size = new Size(180, 20), PasswordChar = '*' };

            btnLogin = new Button { Text = "Giriş Et", Location = new System.Drawing.Point(120, 105), Size = new Size(180, 30) };
            btnLogin.Click += BtnLogin_Click;

            lblError = new Label { Location = new System.Drawing.Point(20, 145), Size = new Size(280, 30), ForeColor = System.Drawing.Color.Red, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };

            this.Controls.AddRange(new Control[] { lblUser, txtUsername, lblPass, txtPassword, btnLogin, lblError });
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                lblError.Text = "İstifadəçi adı və şifrə boş ola bilməz!";
                return;
            }

            btnLogin.Enabled = false;
            lblError.Text = "Yoxlanılır...";

            try
            {
                // SSL bypass yalnız konfiqurasiyada AllowSelfSignedCert=true olduqda aktivdir.
                // İşçilərin kompüterlərindəki appsettings.json-da false olmalıdır.
                var handler = new HttpClientHandler();
                if (_config.AllowSelfSignedCert)
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                using var client = new HttpClient(handler);

                // _config.FullTokenUrl → BaseUrl.TrimEnd('/') + "/api/desktop/token"
                // Bu şəkildə BaseUrl sonunda "/" olsa belə ikiqat slash yaranmır.
                var response = await client.PostAsJsonAsync(
                    _config.FullTokenUrl,
                    new { username = txtUsername.Text, password = txtPassword.Text });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    Token  = result.GetProperty("token").GetString();
                    IsciAd = result.GetProperty("ad").GetString();
                    IsciId = result.GetProperty("isciId").GetInt32();

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    lblError.Text = "İstifadəçi adı və ya şifrə yanlışdır.";
                }
                else if ((int)response.StatusCode == 429)
                {
                    lblError.Text = "Hesab müvəqqəti bloklanıb. Bir az sonra cəhd edin.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    lblError.Text = "Bu hesab masaüstü agentə icazəsizdir (işçiyə bağlı deyil).";
                }
                else
                {
                    lblError.Text = $"Server xətası: {(int)response.StatusCode} {response.StatusCode}";
                }
            }
            catch (HttpRequestException ex)
            {
                lblError.Text = $"Serverə qoşulmaq mümkün olmadı: {ex.Message}";
            }
            catch (Exception ex)
            {
                lblError.Text = $"Xəta: {ex.Message}";
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }
    }
}
