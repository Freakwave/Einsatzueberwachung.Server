// Quelle: WPF-Projekt Models/Team.cs
// Repräsentiert ein Team (Hund+Hundeführer+Helfer oder Drohnenteam) mit Timer und Warnungen
// Hinweis: Timer-Logik wird zentral durch TeamTimerService gesteuert (ein Timer für alle Teams)

using System;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Models
{
    public class Team : IDisposable
    {
        private bool _disposed = false;

        // Startzeit des Timers, gesetzt von StartTimer() und vom TeamTimerService gelesen
        public DateTime StartTime { get; private set; }

        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string DogName { get; set; }
        public string DogId { get; set; }
        public DogSpecialization DogSpecialization { get; set; }
        public string HundefuehrerName { get; set; }
        public string HundefuehrerId { get; set; }
        public string HelferName { get; set; }
        public string HelferId { get; set; }
        public string SearchAreaName { get; set; }
        public string SearchAreaId { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public bool IsRunning { get; set; }
        public bool IsFirstWarning { get; set; }
        public bool IsSecondWarning { get; set; }
        public int FirstWarningMinutes { get; set; }
        public int SecondWarningMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; }

        public bool IsDroneTeam { get; set; }
        public string DroneType { get; set; }
        public string DroneId { get; set; }
        public bool IsSupportTeam { get; set; }

        public event Action<Team>? TimerStarted;
        public event Action<Team>? TimerStopped;
        public event Action<Team>? TimerReset;
        public event Action<Team, bool>? WarningTriggered;
        public event Action<Team>? TimerTick;

        public Team()
        {
            TeamId = Guid.NewGuid().ToString();
            TeamName = string.Empty;
            DogName = string.Empty;
            DogId = string.Empty;
            HundefuehrerName = string.Empty;
            HundefuehrerId = string.Empty;
            HelferName = string.Empty;
            HelferId = string.Empty;
            SearchAreaName = string.Empty;
            SearchAreaId = string.Empty;
            ElapsedTime = TimeSpan.Zero;
            IsRunning = false;
            IsFirstWarning = false;
            IsSecondWarning = false;
            FirstWarningMinutes = 45;
            SecondWarningMinutes = 60;
            CreatedAt = DateTime.Now;
            Notes = string.Empty;
            IsDroneTeam = false;
            DroneType = string.Empty;
            DroneId = string.Empty;
            IsSupportTeam = false;
        }

        public void StartTimer()
        {
            if (!IsRunning)
            {
                StartTime = DateTime.Now - ElapsedTime;
                IsRunning = true;
                TimerStarted?.Invoke(this);
            }
        }

        public void StopTimer()
        {
            if (IsRunning)
            {
                IsRunning = false;
                TimerStopped?.Invoke(this);
            }
        }

        public void ResetTimer()
        {
            StopTimer();
            ElapsedTime = TimeSpan.Zero;
            IsFirstWarning = false;
            IsSecondWarning = false;
            TimerReset?.Invoke(this);
        }

        public void CheckWarnings()
        {
            var totalMinutes = (int)ElapsedTime.TotalMinutes;

            if (!IsFirstWarning && totalMinutes >= FirstWarningMinutes)
            {
                IsFirstWarning = true;
                WarningTriggered?.Invoke(this, false);
            }

            if (!IsSecondWarning && totalMinutes >= SecondWarningMinutes)
            {
                IsSecondWarning = true;
                WarningTriggered?.Invoke(this, true);
            }
        }

        // Aufgerufen vom zentralen TeamTimerService einmal pro Sekunde
        public void Tick(DateTime now)
        {
            if (!IsRunning) return;
            ElapsedTime = now - StartTime;
            CheckWarnings();
            TimerTick?.Invoke(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                IsRunning = false;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
