using Newtonsoft.Json.Linq;
using Q42.HueApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Huemongous
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LightPage : Page
    {
        string lightId;
        Light light;
        bool justLoaded = true;
        HttpClient httpClient;
        DateTime sunrise;
        DateTime sunset;

        public LightPage()
        {
            this.InitializeComponent();

            httpClient = new HttpClient();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            lightId = (string)e.Parameter;
            light = await AppConstants.HueClient.GetLightAsync(lightId);
            lightSwitch.IsOn = light.State.On;
            justLoaded = false;
            await GetTimeOfDay();
            UpdateLight();
            base.OnNavigatedTo(e);
        }

        async Task GetTimeOfDay()
        {
            Geolocator geolocator = new Geolocator();
            geolocator.AllowFallbackToConsentlessPositions();
            Geoposition geoposition = await geolocator.GetGeopositionAsync();
            HttpResponseMessage astronomyResponse = await httpClient.GetAsync(
                    $"http://api.wunderground.com/api/{Secrets.WundergroundApiKey}/astronomy/q/{geoposition.Coordinate.Point.Position.Latitude},{geoposition.Coordinate.Point.Position.Longitude}.json");
            JObject o = JObject.Parse(await astronomyResponse.Content.ReadAsStringAsync());
            sunrise = DateTimeHelper(int.Parse((string)o["moon_phase"]["sunrise"]["hour"]), int.Parse((string)o["moon_phase"]["sunrise"]["minute"]));
            sunset = DateTimeHelper(int.Parse((string)o["moon_phase"]["sunset"]["hour"]), int.Parse((string)o["moon_phase"]["sunset"]["minute"]));
        }

        async Task UpdateLight()
        {
            while (true)
            {
                if (!light.State.On)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    continue;
                }
                DateTime startTime = sunset - TimeSpan.FromMinutes(30);
                DateTime endTime = sunset + TimeSpan.FromMinutes(30);
                if (DateTime.Now >= startTime && DateTime.Now <= endTime)
                {
                    int startingTemp = 6500;
                    int endingTemp = 2000;
                    TimeSpan transitionTime = TimeSpan.FromHours(1);
                    double amountPerMin = (startingTemp - endingTemp) / transitionTime.TotalMinutes;
                    double amount = (DateTime.Now - startTime).TotalMinutes * amountPerMin;
                    double desiredTemp = startingTemp - amount;
                    int mired = HelperMethods.GetMired((int)desiredTemp);
                    LightCommand command = new LightCommand();
                    command.ColorTemperature = mired;
                    command.TransitionTime = TimeSpan.FromSeconds(60);
                    AppConstants.HueClient.SendCommandAsync(command, new List<string> { light.Id });
                }
                else if (DateTime.Now > endTime)
                {
                    LightCommand command = new LightCommand();
                    command.ColorTemperature = HelperMethods.GetMired(2000);
                    AppConstants.HueClient.SendCommandAsync(command, new List<string> { light.Id });
                }
                else if (DateTime.Now < startTime)
                {
                    LightCommand command = new LightCommand();
                    command.ColorTemperature = HelperMethods.GetMired(6500);
                    AppConstants.HueClient.SendCommandAsync(command, new List<string> { light.Id });
                }
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }

        private DateTime DateTimeHelper(int hour, int minute)
        {
            DateTime now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        }

        private void fluxRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            tempComboBox.Visibility = Visibility.Visible;
        }

        private void fluxRadioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            tempComboBox.Visibility = Visibility.Collapsed;
        }

        private async void lightSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (lightSwitch.IsOn)
            {
                LightCommand command = new LightCommand();
                command.On = true;
                AppConstants.HueClient.SendCommandAsync(command, new List<string>() { light.Id });
            }
            else
            {
                LightCommand command = new LightCommand();
                command.On = false;
                AppConstants.HueClient.SendCommandAsync(command, new List<string>() { light.Id });
            }
            await UpdateLightObj();
        }

        private void tempComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (justLoaded)
            {
                return;
            }
            int selectedTemp = int.Parse((string)((ComboBoxItem)tempComboBox.SelectedItem).Tag);
            int mired = HelperMethods.GetMired(selectedTemp);
            LightCommand command = new LightCommand();
            command.ColorTemperature = mired;
            AppConstants.HueClient.SendCommandAsync(command, new List<string>() { light.Id });
        }

        

        async Task UpdateLightObj()
        {
            light = await AppConstants.HueClient.GetLightAsync(lightId);
        }
    }
}
