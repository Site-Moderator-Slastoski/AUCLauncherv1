using System.Net.Http;
using System.Net;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YourNamespace
{
    public partial class MainWindow : Window
    {
        private const string AuthorizationUrl = "https://discord.com/api/oauth2/authorize";
        private const string TokenUrl = "https://discord.com/api/oauth2/token";
        private const string UserInfoUrl = "https://discord.com/api/users/@me";
        private const string ClientId = "1317587040333991989";
        private const string ClientSecret = "1im0V3hxZ8t9eKj2m3AC6iKAlQz-wwTv\r\n";
        private const string RedirectUri = "http://localhost:5000/callback";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CustomTitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) // Double-click to maximize/restore
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove(); // Allow dragging
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private bool isLoginClicked = false;

        private void LoginButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isLoginClicked)
            {
                Mouse.OverrideCursor = Cursors.No;
            }
        }

        private void LoginButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isLoginClicked)
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }

        private async void StartDiscordLogin()
        {
            Mouse.OverrideCursor = Cursors.No;

            isLoginClicked = true;
            LoginButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4f4f4f"));
            LoginButton.Content = "Logging in...";

            string authUrl = $"{AuthorizationUrl}?client_id={ClientId}" +
                             $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                             "&response_type=code&scope=identify";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            string authorizationCode = await StartHttpListenerForCode();

            if (!string.IsNullOrEmpty(authorizationCode))
            {
                await ExchangeCodeForToken(authorizationCode);
            }
        }

        private async System.Threading.Tasks.Task<string> StartHttpListenerForCode()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"{RedirectUri}/");
            listener.Start();

            var context = await listener.GetContextAsync();
            var code = System.Web.HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code");

            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Failed to retrieve authorization code.");
                return null;
            }

            var response = context.Response;
            string responseString = "<html><body>Discord login successful! You may close this tab and return to the launcher.</body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            listener.Stop();
            return code;
        }

        private async System.Threading.Tasks.Task ExchangeCodeForToken(string code)
        {
            var client = new HttpClient();
            var values = new Dictionary<string, string>
    {
        { "client_id", ClientId },
        { "client_secret", ClientSecret },
        { "code", code },
        { "grant_type", "authorization_code" },
        { "redirect_uri", RedirectUri },
    };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(TokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
                string accessToken = jsonResponse["access_token"]?.ToString(); // Use null-conditional operator

                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Use the access token to fetch user data
                    await GetUserInfo(accessToken);
                }
                else
                {
                    MessageBox.Show("Failed to retrieve access token. Token is null.");
                }
            }
            else
            {
                MessageBox.Show("Failed to retrieve access token.");
            }
        }

        private async System.Threading.Tasks.Task GetUserInfo(string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(UserInfoUrl);

            if (response.IsSuccessStatusCode)
            {
                var userInfo = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
                string username = userInfo["username"]?.ToString(); // Use null-conditional operator

                if (!string.IsNullOrEmpty(username))
                {
                    LoginButton.Visibility = Visibility.Collapsed;

                    // Slide MainWindow contents out
                    AnimateSlideOut(MainWindowContents, -MainWindowContents.ActualWidth);

                    // After main content slides out, slide in "Page1"
                    await System.Threading.Tasks.Task.Delay(1000);
                    Page1Contents.Visibility = Visibility.Visible;
                    WelcomeText.Text = $"{username}";
                    AnimateSlideIn(Page1Contents, 0);
                }
                else
                {
                    MessageBox.Show("Failed to retrieve username.");
                }
            }
            else
            {
                MessageBox.Show("Failed to retrieve user info.");
            }
        }

        // Method for sliding out an element
        private void AnimateSlideOut(UIElement element, double toValue)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = element.RenderTransform.Value.OffsetX,
                To = toValue,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        // Method for sliding in an element
        private void AnimateSlideIn(UIElement element, double fromValue)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = fromValue,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
    }
}
