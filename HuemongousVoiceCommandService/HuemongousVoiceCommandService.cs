using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.VoiceCommands;
using Windows.ApplicationModel.Resources.Core;
using Windows.ApplicationModel.AppService;
using System.Diagnostics;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Huemongous.VoiceCommands
{
    public sealed class HuemongousVoiceCommandService : IBackgroundTask
    {
        private string baseUrl = $"http://192.168.1.100/api/q76ofnnJ1jWGs3f6ExPRGL0aWAjQPG5Jdee7dxbq";

        private BackgroundTaskDeferral serviceDeferral;
        private VoiceCommandServiceConnection voiceServiceConnection;
        private HttpClient httpClient;

        public async void RunOther(IBackgroundTaskInstance taskInstance)
        {
            serviceDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            httpClient = new HttpClient();

            AppServiceTriggerDetails triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            if (triggerDetails != null && triggerDetails.Name == "HuemongousVoiceCommandService")
            {
                voiceServiceConnection = VoiceCommandServiceConnection.FromAppServiceTriggerDetails(triggerDetails);
                voiceServiceConnection.VoiceCommandCompleted += VoiceServiceConnection_VoiceCommandCompleted;

                Debug.WriteLine("trying to get voice command");
                VoiceCommand voiceCommand = await voiceServiceConnection.GetVoiceCommandAsync();

                if (voiceCommand.CommandName == "LightsOnOrOff")
                {
                    string lightState = voiceCommand.Properties["lightState"][0];
                    await HandleLightsOnOrOff(lightState);
                }
            }
        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            serviceDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            httpClient = new HttpClient();

            AppServiceTriggerDetails triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if (triggerDetails != null && triggerDetails.Name == "HuemongousVoiceCommandService")
            {
                voiceServiceConnection = VoiceCommandServiceConnection.FromAppServiceTriggerDetails(triggerDetails);
                voiceServiceConnection.VoiceCommandCompleted += VoiceServiceConnection_VoiceCommandCompleted;

                VoiceCommand voiceCommand = await voiceServiceConnection.GetVoiceCommandAsync();

                if (voiceCommand.CommandName == "LightsOnOrOff")
                {
                    string lightState = voiceCommand.Properties["lightState"][0];
                    await HandleLightsOnOrOff(lightState);
                }
                else if (voiceCommand.CommandName == "LightOnOrOff")
                {
                    string lightState = voiceCommand.Properties["lightState"][0];
                    string lightOrRoom = voiceCommand.Properties["lightOrRoom"][0];
                    string lightPlurality = voiceCommand.Properties["lightPlurality"][0];
                    await HandleLightOnOrOff(lightState, lightOrRoom, lightPlurality);
                }
                else if (voiceCommand.CommandName == "SpecifyLightOnOrOff")
                {
                    string lightState = voiceCommand.Properties["lightState"][0];
                }
                else if (voiceCommand.CommandName == "SetLightScene")
                {
                    string scene = voiceCommand.Properties["scene"][0];
                }
                else if (voiceCommand.CommandName == "SetLightsColor")
                {
                    string color = voiceCommand.Properties["color"][0];
                }
                else if (voiceCommand.CommandName == "SetLightColor")
                {
                    string lightOrRoom = voiceCommand.Properties["lightOrRoom"][0];
                    string lightPlurality = voiceCommand.Properties["lightPlurality"][0];
                    string color = voiceCommand.Properties["color"][0];
                }
                else if (voiceCommand.CommandName == "SpecifyLightColor")
                {
                    string color = voiceCommand.Properties["color"][0];
                }
                else
                {
                    Debug.WriteLine("unknown command");
                }
            }
        }

        private async Task ShowProgressScreen(string message)
        {
            VoiceCommandUserMessage userProgressMessage = new VoiceCommandUserMessage()
            {
                DisplayMessage = message,
                SpokenMessage = message
            };

            VoiceCommandResponse response = VoiceCommandResponse.CreateResponse(userProgressMessage);
            await voiceServiceConnection.ReportProgressAsync(response);
        }

        private async Task HandleLightsOnOrOff(string lightState)
        {
            await ShowProgressScreen("Hold on");

            VoiceCommandUserMessage userMessage = new VoiceCommandUserMessage();

            string defaultMessage = $"Turning your lights {lightState}";

            JObject on = new JObject();
            try
            {
                SetOnOrOff(lightState, on);
                httpClient.PutAsync($"{baseUrl}/groups/0/action", new StringContent(on.ToString()));
                userMessage.DisplayMessage = defaultMessage;
                userMessage.SpokenMessage = defaultMessage;
            }
            catch (Exception ex)
            {
                SetError(userMessage);
                VoiceCommandResponse errResponse = VoiceCommandResponse.CreateResponse(userMessage);
                await voiceServiceConnection.ReportFailureAsync(errResponse);
            }

            VoiceCommandResponse response = VoiceCommandResponse.CreateResponse(userMessage);
            await voiceServiceConnection.ReportSuccessAsync(response);
        }

        private async Task HandleLightOnOrOff(string lightState, string lightOrRoom, string lightPlurality)
        {
            await ShowProgressScreen("Hold on");
            VoiceCommandUserMessage userMessage = new VoiceCommandUserMessage();
            string defaultMessage = $"Turning your {lightOrRoom} {lightPlurality} {lightState}";

            JObject command = new JObject();
            try
            {
                SetOnOrOff(lightState, command);
            }
            catch (Exception ex)
            {
                SetError(userMessage);
                VoiceCommandResponse errResponse = VoiceCommandResponse.CreateResponse(userMessage);
                await voiceServiceConnection.ReportFailureAsync(errResponse);
                return;
            }

            // light name -> light ID
            Dictionary<string, string> lights = new Dictionary<string, string>();
            JObject lightsResponseObject = JObject.Parse(await (await httpClient.GetAsync($"{baseUrl}/lights")).Content.ReadAsStringAsync());
            foreach (var light in lightsResponseObject)
            {
                JObject lightObject = (JObject)light.Value;
                lights.Add(((string)lightObject["name"]).ToLower(), light.Key);
            }

            // roon name -> room ID
            Dictionary<string, string> rooms = new Dictionary<string, string>();
            JObject roomsResponseObject = JObject.Parse(await (await httpClient.GetAsync($"{baseUrl}/groups")).Content.ReadAsStringAsync());
            foreach (var room in roomsResponseObject)
            {
                JObject groupObject = (JObject)room.Value;
                rooms.Add(((string)groupObject["name"]).ToLower(), room.Key);
            }

            string matchingLight = null;
            string matchingRoom = null;

            foreach (var light in lights)
            {
                if (light.Key == lightOrRoom)
                {
                    matchingLight = light.Value;
                    break;
                }
            }
            foreach (var room in rooms)
            {
                if (room.Key == lightOrRoom)
                {
                    matchingRoom = room.Value;
                }
            }

            if (!string.IsNullOrEmpty(matchingLight) && string.IsNullOrEmpty(matchingRoom))
            {
                httpClient.PutAsync($"{baseUrl}/lights/{matchingLight}/state", new StringContent(command.ToString()));
                userMessage.DisplayMessage = defaultMessage;
                userMessage.SpokenMessage = defaultMessage;
            }
            else if (string.IsNullOrEmpty(matchingLight) && !string.IsNullOrEmpty(matchingRoom))
            {
                httpClient.PutAsync($"{baseUrl}/groups/{matchingRoom}/action", new StringContent(command.ToString()));
                userMessage.DisplayMessage = defaultMessage;
                userMessage.SpokenMessage = defaultMessage;
            }
            else if (!string.IsNullOrEmpty(matchingLight) && !string.IsNullOrEmpty(matchingRoom))
            {
                if (lightPlurality == "light")
                {
                    httpClient.PutAsync($"{baseUrl}/lights/{matchingLight}/state", new StringContent(command.ToString()));
                    userMessage.DisplayMessage = defaultMessage;
                    userMessage.SpokenMessage = defaultMessage;
                }
                else if (lightPlurality == "lights")
                {
                    httpClient.PutAsync($"{baseUrl}/groups/{matchingRoom}/action", new StringContent(command.ToString()));
                    userMessage.DisplayMessage = defaultMessage;
                    userMessage.SpokenMessage = defaultMessage;
                }
                else
                {
                    Debug.WriteLine("user didn't say light or lights");
                    SetError(userMessage);
                    VoiceCommandResponse errResponse = VoiceCommandResponse.CreateResponse(userMessage);
                    await voiceServiceConnection.ReportFailureAsync(errResponse);
                    return;
                }
            }
            else
            {
                Debug.WriteLine("Could not find light or room");
                SetError(userMessage);
                VoiceCommandResponse errResponse = VoiceCommandResponse.CreateResponse(userMessage);
                await voiceServiceConnection.ReportFailureAsync(errResponse);
                return;
            }

            VoiceCommandResponse response = VoiceCommandResponse.CreateResponse(userMessage);
            await voiceServiceConnection.ReportSuccessAsync(response);
        }

        private void SetError(VoiceCommandUserMessage userMessage)
        {
            userMessage.DisplayMessage = "I didn't get that";
            userMessage.SpokenMessage = "I didn't get that";
        }

        private void SetOnOrOff(string lightState, JObject o)
        {
            if (lightState == "on")
            {
                o["on"] = true;
            }
            else if (lightState == "off")
            {
                o["on"] = false;
            }
            else
            {
                throw new Exception("Light state is not valid");
            }
        }

        private void VoiceServiceConnection_VoiceCommandCompleted(VoiceCommandServiceConnection sender, VoiceCommandCompletedEventArgs args)
        {
            Debug.WriteLine("Completed");
            if (serviceDeferral != null)
            {
                serviceDeferral.Complete();
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine($"Canceled: {reason.ToString()}");
            if (serviceDeferral != null)
            {
                serviceDeferral.Complete();
            }
        }
    }
}
