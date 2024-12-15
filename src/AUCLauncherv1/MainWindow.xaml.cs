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
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace YourNamespace
{
    public partial class MainWindow : Window
    {
        private const string AuthorizationUrl = "https://discord.com/oauth2/authorize";
        private const string TokenUrl = "https://discord.com/api/oauth2/token";
        private const string UserInfoUrl = "https://discord.com/api/users/@me";
        private const string ClientId = "1317587040333991989";
        private const string ClientSecret = "client_secret";
        private const string RedirectUri = "http://localhost:5001/callback";
        private List<string> imagePaths;
        private int currentImageIndex;
        private DispatcherTimer slideshowTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSlideshow();
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

        private void StartGridAnimations()
        {
            var element1Animation = new ThicknessAnimation
            {
                From = new Thickness(0),
                To = new Thickness(-ActualWidth, 0, ActualWidth, 0),
                Duration = TimeSpan.FromSeconds(0.5)
            };

            var page1Animation = new ThicknessAnimation
            {
                From = new Thickness(ActualWidth, 0, -ActualWidth, 0),
                To = new Thickness(0),
                Duration = TimeSpan.FromSeconds(0.5)
            };

            Element__1.BeginAnimation(MarginProperty, element1Animation);
            Page1Contents.BeginAnimation(MarginProperty, page1Animation);
        }

        private void InitializeSlideshow()
        {
            imagePaths = new List<string>
            {
                "Assets/Slideshow Images/image1.png",
                "Assets/Slideshow/image2.png",
                "Assets/Slideshow Images/image3.jpg",
                "Assets/Slideshow Images/image4.jpg",
                "Assets/Slideshow Images/image5.jpg"
            };

            currentImageIndex = 0;

            slideshowTimer = new DispatcherTimer();
            slideshowTimer.Interval = TimeSpan.FromSeconds(3);
            slideshowTimer.Tick += SlideshowTimer_Tick;
            slideshowTimer.Start();

            ShowImage();
        }

        private void SlideshowTimer_Tick(object sender, EventArgs e)
        {
            currentImageIndex++;
            if (currentImageIndex >= imagePaths.Count)
            {
                currentImageIndex = 0;
            }

            ShowImage();
        }

        private void ShowImage()
        {
            BitmapImage bitmap = new BitmapImage(new Uri(imagePaths[currentImageIndex], UriKind.Relative));
            SlideshowImage.Source = bitmap;
        }

        private async void StartDiscordLogin(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.No;
            LoginButton.Content = "Logging in...";
            isLoginClicked = true;

            string authUrl = $"{AuthorizationUrl}?client_id={ClientId}" +
                             $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                             "&response_type=code&scope=identify";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open the browser: {ex.Message}");
                isLoginClicked = false;
                LoginButton.Content = "Login via Discord";
                return;
            }

            string authorizationCode = await StartHttpListenerForCode();

            if (!string.IsNullOrEmpty(authorizationCode))
            {
                bool isTokenExchanged = await ExchangeCodeForToken(authorizationCode);
                if (isTokenExchanged)
                {
                    StartGridAnimations();
                }
                else
                {
                    MessageBox.Show("Login failed. Please try again.");
                }
            }
            else
            {
                MessageBox.Show("Authorization failed. Please try again.");
            }

            isLoginClicked = false;
            LoginButton.Content = "Login via Discord";
        }

        private async Task<string> StartHttpListenerForCode()
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"{RedirectUri}/");
                listener.Start();

                var context = await listener.GetContextAsync();
                var code = System.Web.HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code");

                if (string.IsNullOrEmpty(code))
                {
                    MessageBox.Show("Failed to retrieve authorization code.");
                    isLoginClicked = false;
                    LoginButton.Content = "Login via Discord";
                    listener.Stop();
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error during HTTP listener setup: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> ExchangeCodeForToken(string code)
        {
            try
            {
                var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    { "client_id", ClientId },
                    { "client_secret", ClientSecret },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", RedirectUri }
                };

                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync(TokenUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to retrieve access token. Error: {errorDetails}");
                    return false;
                }

                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());

                if (!jsonResponse.TryGetValue("access_token", out JToken token) || string.IsNullOrEmpty(token?.ToString()))
                {
                    MessageBox.Show("Access token not found in the response.");
                    return false;
                }

                string accessToken = token.ToString();
                await GetUserInfo(accessToken);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while exchanging code for token: {ex.Message}");
                return false;
            }
        }

        private async Task GetUserInfo(string accessToken)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(UserInfoUrl);

                if (response.IsSuccessStatusCode)
                {
                    var userInfo = JObject.Parse(await response.Content.ReadAsStringAsync());
                    string username = userInfo["username"]?.ToString();

                    if (!string.IsNullOrEmpty(username))
                    {
                        WelcomeText.Text = $"{username}";
                    }
                    else
                    {
                        MessageBox.Show("Failed to retrieve username.");
                        isLoginClicked = false;
                        LoginButton.Content = "Login via Discord";
                    }
                }
                else
                {
                    MessageBox.Show("Failed to retrieve user info.");
                    isLoginClicked = false;
                    LoginButton.Content = "Login via Discord";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while retrieving user info: {ex.Message}");
                isLoginClicked = false;
                LoginButton.Content = "Login via Discord";
            }
        }
    }
}
