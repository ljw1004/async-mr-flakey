using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Windows_CSharp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            MrFlakey.Start();
        }

        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                progress1.IsIndeterminate = true;
                button1.IsEnabled = false;
                label4.Text = "";
                var http = new HttpClient();
                //
                var ip = await GetPublicIPAsync(http);
                label1.Text = String.Format("IP: {0}", ip);
                //
                var loc = await GetLongLatAsync(http, ip);
                double longitude = loc.Item1, latitude = loc.Item2;
                label2.Text = String.Format("LAT: {0}, LONG: {1}", latitude, longitude);
                //
                var weather = await GetCurrentWeatherAsync(http, longitude, latitude);
                label3.Text = String.Format("TEMP: {0}, DESC: {1}", weather.Item1, weather.Item2);
                image1.Source = new BitmapImage(weather.Item3);
            }
            catch (Exception ex)
            {
                label4.Text = String.Format("EXCEPTION: {0}\r\n{1}", ex.Message, ex.StackTrace);
            }
            finally
            {
                progress1.IsIndeterminate = false;
                button1.IsEnabled = true;
            }
        }

        static async Task<string> GetPublicIPAsync(HttpClient http)
        {
            // NOT IPv6 FRIENDLY!
            var uri = "http://ipinfo.io/ip";
            var ip = await http.GetStringAsync(uri).Flakey();
            ip = ip.Trim();
            if (!Regex.IsMatch(ip, "^\\d+\\.\\d+\\.\\d+\\.\\d+$\\z")) throw new Exception("Unrecognized IP \"" + ip + "\"");
            return ip;
        }

        static async Task<Tuple<double, double>> GetLongLatAsync(HttpClient http, string ip)
        {
            var uri = "http://freegeoip.net/xml/" + ip;
            var loc = await http.GetStringAsync(uri).Flakey();
            var xloc = XDocument.Parse(loc);
            var longitude = Double.Parse(xloc.Element("Response").Element("Longitude").Value);
            var latitude = Double.Parse(xloc.Element("Response").Element("Latitude").Value);
            return Tuple.Create(longitude, latitude);
        }

        static async Task<Tuple<double, string, Uri>> GetCurrentWeatherAsync(HttpClient http, double longitude, double latitude)
        {
            var uri = "http://api.openweathermap.org/data/2.5/weather?mode=xml&units=metric&lat=" + latitude.ToString() + "&lon=" + longitude.ToString();
            var weather = await http.GetStringAsync(uri).Flakey();
            var xweather = XDocument.Parse(weather);
            var temp = Double.Parse(xweather.Element("current").Element("temperature").Attribute("value").Value);
            var desc = xweather.Element("current").Element("weather").Attribute("value").Value;
            var icon = xweather.Element("current").Element("weather").Attribute("icon").Value;
            if (!Regex.IsMatch(icon, "^[0-9a-zA-Z]+$\\z")) throw new FormatException("unrecognized icon");
            icon = "http://openweathermap.org/img/w/" + icon + ".png";
            return Tuple.Create(temp, desc, new Uri(icon, UriKind.Absolute));
        }


    }
}
