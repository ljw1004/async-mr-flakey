Imports System.Net.Http
Imports System.Text.RegularExpressions

' The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page

    Sub New()
        InitializeComponent()
        MrFlakey.Start()
    End Sub

    Private Async Sub button1_Click(sender As Object, e As RoutedEventArgs)
        Try
            progress1.IsIndeterminate = True
            button1.IsEnabled = False
            label4.Text = ""
            Dim http As New HttpClient()
            '
            Dim ip = Await GetPublicIPAsync(http)
            label1.Text = String.Format("IP: {0}", ip)
            '
            Dim loc = Await GetLongLatAsync(http, ip)
            Dim longitude = loc.Item1, latitude = loc.Item2
            label2.Text = String.Format("LAT: {0}, LONG: {1}", latitude, longitude)
            '
            Dim weather = Await GetCurrentWeatherAsync(http, longitude, latitude)
            label3.Text = String.Format("TEMP: {0}, DESC: {1}", weather.Item1, weather.Item2)
            image1.Source = New BitmapImage(weather.Item3)
        Catch ex As Exception
            label4.Text = String.Format("EXCEPTION: {0}{1}{2}", ex.Message, vbCrLf, ex.StackTrace)
        Finally
            progress1.IsIndeterminate = False
            button1.IsEnabled = True
        End Try
    End Sub


    Shared Async Function GetPublicIPAsync(http As HttpClient) As Task(Of String)
        ' Not IPv6 FRIENDLY!
        Dim uri = "http://ipinfo.io/ip"
        Dim ip = Await http.GetStringAsync(uri).Flakey()
        ip = ip.Trim()
        If Not Regex.IsMatch(ip, "^\d+\.\d+\.\d+\.\d+$\z") Then Throw New Exception("Unrecognized IP """ & ip & """")
        Return ip
    End Function

    Shared Async Function GetLongLatAsync(http As HttpClient, ip As String) As Task(Of Tuple(Of Double, Double))
        Dim uri = "http://freegeoip.net/xml/" & ip
        Dim loc = Await http.GetStringAsync(uri).Flakey()
        Dim xloc = XDocument.Parse(loc)
        Dim longitude = Double.Parse(xloc.<Response>.<Longitude>.Value)
        Dim latitude = Double.Parse(xloc.<Response>.<Latitude>.Value)
        Return Tuple.Create(longitude, latitude)
    End Function


    Shared Async Function GetCurrentWeatherAsync(http As HttpClient, longitude As Double, latitude As Double) As Task(Of Tuple(Of Double, String, Uri))
        Dim uri = "http://api.openweathermap.org/data/2.5/weather?mode=xml&units=metric&lat=" & latitude & "&lon=" & longitude
        Dim weather = Await http.GetStringAsync(uri).Flakey()
        Dim xweather = XDocument.Parse(weather)
        Dim temp = Double.Parse(xweather.<current>.<temperature>.@value)
        Dim desc = xweather.<current>.<weather>.@value
        Dim icon = xweather.<current>.<weather>.@icon
        If Not Regex.IsMatch(icon, "^[0-9a-zA-Z]+$\z") Then Throw New FormatException("unrecognized icon")
        icon = "http://openweathermap.org/img/w/" & icon & ".png"
        Return Tuple.Create(temp, desc, New Uri(icon, UriKind.Absolute))
    End Function

End Class
