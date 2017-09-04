using Q42.HueApi;
using Q42.HueApi.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using System.Diagnostics;
using Windows.ApplicationModel.VoiceCommands;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.UI;
using System.Text;
using System.Reflection;

namespace Huemongous
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await OnLaunchedOrActivated(e);
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await OnLaunchedOrActivated(args);
        }

        private async Task OnLaunchedOrActivated(IActivatedEventArgs e)
        {
            try
            {
                StorageFile vcdStorageFile = await Package.Current.InstalledLocation.GetFileAsync("VCDs\\HuemongousCommands.xml");
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(vcdStorageFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Installing voice commands failed: {ex.ToString()}");
            }
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.Kind == ActivationKind.Launch)
            {
                LaunchActivatedEventArgs args = e as LaunchActivatedEventArgs;
                if (args.PrelaunchActivated == false)
                {
                    if (rootFrame.Content == null)
                    {
                        IBridgeLocator locator = new HttpBridgeLocator();
                        var ips = (await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5))).ToList();
                        if (ips.Count > 0)
                        {
                            AppConstants.HueClient = new LocalHueClient(ips[0].IpAddress, Secrets.HueUsername);
                        }

                        // When the navigation stack isn't restored navigate to the first page,
                        // configuring the new page by passing required information as a navigation
                        // parameter
                        rootFrame.Navigate(typeof(MainPage), args.Arguments);
                    }
                }

                if (!string.IsNullOrEmpty(args.Arguments))
                {
                    string lightToToggle = args.Arguments;
                    var light = await AppConstants.HueClient.GetLightAsync(lightToToggle);
                    if (light.State.On)
                    {
                        LightCommand command = new LightCommand();
                        command.On = false;
                        await AppConstants.HueClient.SendCommandAsync(command, new List<string> { lightToToggle });
                        Exit();
                    }
                    else
                    {
                        LightCommand command = new LightCommand();
                        command.On = true;
                        command.Brightness = 128;
                        command.ColorTemperature = HelperMethods.GetMired(3200);
                        await AppConstants.HueClient.SendCommandAsync(command, new List<string> { lightToToggle });
                        Exit();
                    }
                }
            }

            // Update lights, rooms, scenes, and colors in VCD
            try
            {
                VoiceCommandDefinition vcd;
                if (VoiceCommandDefinitionManager.InstalledCommandDefinitions.TryGetValue("HuemongousCommandSet", out vcd))
                {
                    List<string> lightOrRoom = new List<string>();

                    var lights = await AppConstants.HueClient.GetLightsAsync();
                    foreach (var light in lights)
                    {
                        lightOrRoom.Add(light.Name.ToLower());
                    }

                    var rooms = await AppConstants.HueClient.GetGroupsAsync();
                    foreach (var room in rooms)
                    {
                        lightOrRoom.Add(room.Name.ToLower());
                    }

                    List<string> scenesList = new List<string>();

                    var scenes = await AppConstants.HueClient.GetScenesAsync();
                    foreach (var scene in scenes)
                    {
                        scenesList.Add(scene.Name.ToLower());
                    }

                    List<string> colors = new List<string>();
                    foreach (var value in typeof(Colors).GetTypeInfo().DeclaredProperties)
                    {
                        string color = value.Name;
                        StringBuilder humanReadableColor = new StringBuilder();
                        foreach (var c in color)
                        {
                            // if c is a capitol letter append a space before it
                            if (c > 64 && c < 91)
                            {
                                humanReadableColor.Append(" ");
                            }
                            humanReadableColor.Append(c);
                        }
                        colors.Add(humanReadableColor.ToString().Trim().ToLower());
                    }

                    await vcd.SetPhraseListAsync("lightOrRoom", lightOrRoom);
                    await vcd.SetPhraseListAsync("scene", scenesList);
                    await vcd.SetPhraseListAsync("color", colors);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update phrase list for VCDs: {ex.ToString()}");
            }

            if (e.Kind == ActivationKind.VoiceCommand)
            {
                try
                {
                    VoiceCommandActivatedEventArgs args = e as VoiceCommandActivatedEventArgs;
                    SpeechRecognitionResult speechRecognitionResult = args.Result;

                    string voiceCommandName = speechRecognitionResult.RulePath[0];
                    string textSpoken = speechRecognitionResult.Text;

                    foreach (var prop in speechRecognitionResult.SemanticInterpretation.Properties)
                    {
                        Debug.WriteLine($"Key: {prop.Key}");
                        foreach (var val in prop.Value)
                        {
                            Debug.WriteLine($"Value: {val}");
                        }
                        Debug.WriteLine("");
                    }
                    string commandMode = SemanticInterpretation("commandMode", speechRecognitionResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("help");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

        private string SemanticInterpretation(string interpretationKey, SpeechRecognitionResult speechRecognitionResult)
        {
            return speechRecognitionResult.SemanticInterpretation.Properties[interpretationKey].FirstOrDefault();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
