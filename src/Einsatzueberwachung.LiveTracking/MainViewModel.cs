using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Einsatzueberwachung.LiveTracking.Models;
using Einsatzueberwachung.LiveTracking.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Einsatzueberwachung.LiveTracking
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly GarminUsbService _garminService;
        private readonly ServerApiClient _serverApiClient;
        private readonly Dictionary<int, DogTrackInfo> _dogTrackMap = new();
        private readonly Timer _garminCheckTimer;

        [ObservableProperty]
        private string _statusMessage = "Bereit. Warte auf Garmin-Gerät...";

        [ObservableProperty]
        private bool _isGarminConnected;

        [ObservableProperty]
        private bool _isServerConnected;

        [ObservableProperty]
        private string _serverUrl = string.Empty;

        [ObservableProperty]
        private PvtDataD800? _currentPvtData;

        [ObservableProperty]
        private string _currentUtmString = "N/A";

        [ObservableProperty]
        private string _mainDeviceCoords = "--";

        [ObservableProperty]
        private int _totalLocationsSent;

        [ObservableProperty]
        private int _totalLocationsFailed;

        public ObservableCollection<DogTrackInfo> DogTracks { get; } = new();
        public ObservableCollection<string> LogMessages { get; } = new();

        public MainViewModel()
        {
            _serverApiClient = new ServerApiClient();
            _serverApiClient.StatusChanged += msg => AddLog(msg);

            _garminService = new GarminUsbService();
            _garminService.StatusMessageChanged += OnGarminStatusChanged;
            _garminService.IsConnectedChanged += OnGarminConnectionChanged;
            _garminService.MainDevicePvtUpdated += OnMainDevicePvtUpdated;
            _garminService.DogDataUpdated += OnDogDataUpdated;

            // Start checking for the Garmin device immediately, then every 5 seconds.
            _garminCheckTimer = new Timer(OnGarminCheckTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private void OnGarminCheckTimerTick(object? state)
        {
            if (!IsGarminConnected && !_garminService.IsProcessing)
            {
                AddLog("Garmin-Gerät nicht verbunden. Starte Verbindungsversuch...");
                _ = TryStartGarminAsync();
            }
        }

        private async Task TryStartGarminAsync()
        {
            try
            {
                await _garminService.StartAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Unerwarteter Garmin-Fehler: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ConnectServerAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                StatusMessage = "Bitte Server-URL eingeben.";
                return;
            }

            _serverApiClient.Configure(ServerUrl);
            AddLog($"Teste Verbindung zu {ServerUrl}...");

            bool connected = await _serverApiClient.TestConnectionAsync();
            IsServerConnected = connected;

            if (connected)
            {
                AddLog("Server-Verbindung erfolgreich.");
                StatusMessage = "Server verbunden.";
                Properties.Settings.Default.ServerUrl = ServerUrl;
                Properties.Settings.Default.Save();
            }
            else
            {
                AddLog("Server nicht erreichbar.");
                StatusMessage = "Server-Verbindung fehlgeschlagen.";
            }
        }

        public void LoadSettings()
        {
            ServerUrl = Properties.Settings.Default.ServerUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ServerUrl))
            {
                _serverApiClient.Configure(ServerUrl);
            }
        }

        private void OnGarminStatusChanged(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = message;
            });
        }

        private void OnGarminConnectionChanged(bool connected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsGarminConnected = connected;
                AddLog(connected ? "Garmin-Gerät verbunden." : "Garmin-Gerät getrennt.");
            });
        }

        private void OnMainDevicePvtUpdated(PvtDataD800 pvtData)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentPvtData = pvtData;
                MainDeviceCoords = $"{pvtData.LatitudeDegrees:F5}°N, {pvtData.LongitudeDegrees:F5}°E";

                if (pvtData.FixType >= 2 && pvtData.LatitudeDegrees >= -80 && pvtData.LatitudeDegrees <= 84)
                {
                    try
                    {
                        CurrentUtmString = CoordinateTranformer.ToUtm(pvtData.LongitudeDegrees, pvtData.LatitudeDegrees).ToString();
                    }
                    catch
                    {
                        CurrentUtmString = "UTM Fehler";
                    }
                }
                else
                {
                    CurrentUtmString = "Kein GPS-Fix";
                }
            });
        }

        private async void OnDogDataUpdated(DogCollarData dogData)
        {
            DogTrackInfo? trackInfo = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(dogData.DogName)) return;

                if (!_dogTrackMap.TryGetValue(dogData.ID, out trackInfo))
                {
                    trackInfo = new DogTrackInfo();
                    _dogTrackMap[dogData.ID] = trackInfo;
                    DogTracks.Add(trackInfo);
                    AddLog($"Neuer Hund erkannt: {dogData.DogName} (ID: {dogData.ID})");
                }

                trackInfo.UpdateFromCollarData(dogData);
            });

            if (trackInfo != null && _serverApiClient.IsConfigured)
            {
                bool success = await _serverApiClient.SendCollarLocationAsync(
                    dogData.ID.ToString(),
                    dogData.DogName,
                    dogData.LatitudeDegrees,
                    dogData.LongitudeDegrees);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (trackInfo != null)
                    {
                        trackInfo.LastSendSuccess = success;
                        if (success)
                        {
                            trackInfo.TotalPointsSent++;
                            TotalLocationsSent++;
                        }
                        else
                        {
                            TotalLocationsFailed++;
                        }
                    }
                });
            }
        }

        private void AddLog(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                LogMessages.Insert(0, entry);
                if (LogMessages.Count > 200)
                    LogMessages.RemoveAt(LogMessages.Count - 1);
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    LogMessages.Insert(0, entry);
                    if (LogMessages.Count > 200)
                        LogMessages.RemoveAt(LogMessages.Count - 1);
                });
            }
        }

        public void Dispose()
        {
            _garminCheckTimer.Dispose();
            _garminService.Dispose();
            _serverApiClient.Dispose();
        }
    }
}

