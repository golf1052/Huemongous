using Q42.HueApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
        Light light;
        bool justLoaded = true;
        public LightPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            light = await AppConstants.HueClient.GetLightAsync((string)e.Parameter);
            lightSwitch.IsOn = light.State.On;
            justLoaded = false;
            base.OnNavigatedTo(e);
        }

        private void fluxRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            tempComboBox.Visibility = Visibility.Visible;
        }

        private void fluxRadioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            tempComboBox.Visibility = Visibility.Collapsed;
        }

        private void lightSwitch_Toggled(object sender, RoutedEventArgs e)
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
        }

        private void tempComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (justLoaded)
            {
                return;
            }
            int selectedTemp = int.Parse((string)((ComboBoxItem)tempComboBox.SelectedItem).Tag);
            int mired = 1000000 / selectedTemp;
            LightCommand command = new LightCommand();
            command.ColorTemperature = mired;
            AppConstants.HueClient.SendCommandAsync(command, new List<string>() { light.Id });
        }
    }
}
