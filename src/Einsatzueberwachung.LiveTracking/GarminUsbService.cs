using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Einsatzueberwachung.LiveTracking
{
        public class GarminUsbService : IDisposable
        {
            private readonly GarminUsbDevice _garminDevice;
            private readonly object _lock = new();
            private CancellationTokenSource? _cts;
            private bool _isProcessing;

            public bool IsProcessing
            {
                get { lock (_lock) { return _isProcessing; } }
            }

            public event Action<PvtDataD800> MainDevicePvtUpdated;
            public event Action<DogCollarData> DogDataUpdated;
            // public event Action<List<TrackedEntityData>> MultiPersonDataUpdated; // If using packet 0x72
            public event Action<string> StatusMessageChanged;
            public event Action<bool> IsConnectedChanged; // To notify connection status

            public GarminUsbService()
            {
                // GarminPacketProcessor is not strictly needed if GarminUsbDevice now dispatches typed data
                _garminDevice = new GarminUsbDevice(null); // Pass null or a simplified processor

                // Configure GarminUsbDevice to call our handlers
                _garminDevice.SetPvtDataHandler(pvtData =>
                    Application.Current.Dispatcher.Invoke(() => MainDevicePvtUpdated?.Invoke(pvtData)));

                _garminDevice.SetDogCollarDataHandler(dogData =>
                    Application.Current.Dispatcher.Invoke(() => DogDataUpdated?.Invoke(dogData)));

                // If you had a multi-person handler for packet 0x72
                // _garminDevice.SetMultiPersonDataHandler(entities =>
                //    Application.Current.Dispatcher.Invoke(() => MultiPersonDataUpdated?.Invoke(entities)));

                _garminDevice.SetStatusMessageHandler(message =>
                    Application.Current.Dispatcher.Invoke(() => StatusMessageChanged?.Invoke(message)));

                _garminDevice.SetUsbProtocolLayerDataHandler(usbHeader =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (usbHeader.PacketType == 0 && usbHeader.ApplicationPacketID == NativeMethods.PID_SESSION_STARTED)
                        {
                            StatusMessageChanged?.Invoke("USB Session Started with device. Sending Start PVT command...");
                            // This command should ideally be sent after confirming session started
                            // and device is ready. GarminUsbDevice can manage this internally.
                            _garminDevice.SendStartPvtDataCommand();
                        }
                    });
                });
            }

            public async Task StartAsync()
            {
                CancellationToken token;
                lock (_lock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;

                    // Dispose the previous token source and create a new one atomically.
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    token = _cts.Token;
                }

                bool wasConnected = false;
                try
                {
                    StatusMessageChanged?.Invoke("Attempting to connect to Garmin device...");

                    bool connected = await Task.Run(() => _garminDevice.Connect(), token);

                    if (connected)
                    {
                        Application.Current.Dispatcher.Invoke(() => IsConnectedChanged?.Invoke(true));
                        StatusMessageChanged?.Invoke("Device connected. Starting listener...");
                        wasConnected = true;
                        // StartListening blocks until the device disconnects or Stop() cancels it.
                        await Task.Run(() => _garminDevice.StartListening(), token);
                    }
                    else
                    {
                        StatusMessageChanged?.Invoke("Could not connect to Garmin device.");
                    }
                }
                catch (OperationCanceledException)
                {
                    StatusMessageChanged?.Invoke("Garmin connection cancelled.");
                }
                catch (Exception ex)
                {
                    StatusMessageChanged?.Invoke($"Garmin error: {ex.Message}");
                }
                finally
                {
                    // Always notify disconnection if we were connected, and always release the
                    // processing flag so the timer can trigger a new attempt.
                    if (wasConnected)
                    {
                        Application.Current.Dispatcher.Invoke(() => IsConnectedChanged?.Invoke(false));
                        StatusMessageChanged?.Invoke("USB listening stopped.");
                    }
                    lock (_lock) { _isProcessing = false; }
                }
            }

            public void Stop()
            {
                CancellationTokenSource? cts;
                lock (_lock)
                {
                    cts = _cts;
                }
                // Cancel the token so the Task.Run inside StartAsync finishes.
                // StartAsync's finally block will reset _isProcessing and fire IsConnectedChanged(false).
                // The CTS may have been disposed by a concurrent StartAsync initialisation;
                // in that case the old token is irrelevant (a new one has replaced it).
                try { cts?.Cancel(); }
                catch (ObjectDisposedException) { }
                _garminDevice.StopListening();
            }

            public void Dispose()
            {
                Stop();
                _garminDevice?.Dispose();
                _cts?.Dispose();
            }
        }
    }
