using Huemongous.Bindings;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Windows.UI.StartScreen;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Huemongous
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<LightListViewBinding> LightsCollection { get; set; }
        

        public MainPage()
        {
            this.InitializeComponent();
            LightsCollection = new ObservableCollection<LightListViewBinding>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var lights = (await AppConstants.HueClient.GetLightsAsync()).ToList();
            foreach (var light in lights)
            {
                LightsCollection.Add(new LightListViewBinding(light.Name, light.Id));
                //if (!SecondaryTile.Exists(light.Id))
                //{
                //    SecondaryTile tile = new SecondaryTile(light.Id, light.Name, $"{light.Id}", new Uri("ms-appx:///Assets/Square150x150Logo.scale-200.png"), TileSize.Square150x150);
                //    tile.VisualElements.ShowNameOnSquare150x150Logo = true;
                //    await tile.RequestCreateAsync();
                //}
            }

            base.OnNavigatedTo(e);
        }

        private void lightListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Frame.Navigate(typeof(LightPage), ((LightListViewBinding)e.ClickedItem).Number);
        }
    }
}
