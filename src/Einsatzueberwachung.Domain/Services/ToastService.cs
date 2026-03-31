// Toast-Notification Service für Benutzer-Feedback
// Ermöglicht das Anzeigen von Toast-Nachrichten von überall in der Anwendung

using System;

namespace Einsatzueberwachung.Domain.Services
{
    public class ToastService
    {
        public event Action<ToastMessage>? OnShow;

        public void ShowSuccess(string message)
        {
            OnShow?.Invoke(new ToastMessage
            {
                Type = ToastType.Success,
                Message = message
            });
        }

        public void ShowError(string message)
        {
            OnShow?.Invoke(new ToastMessage
            {
                Type = ToastType.Error,
                Message = message
            });
        }

        public void ShowWarning(string message)
        {
            OnShow?.Invoke(new ToastMessage
            {
                Type = ToastType.Warning,
                Message = message
            });
        }

        public void ShowInfo(string message)
        {
            OnShow?.Invoke(new ToastMessage
            {
                Type = ToastType.Info,
                Message = message
            });
        }
    }

    public class ToastMessage
    {
        public ToastType Type { get; set; }
        public string Message { get; set; } = "";
    }

    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
