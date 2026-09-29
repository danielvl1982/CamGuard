namespace Enrollment.Domain;

/// <summary>Delivery channel an alert can be sent through. A role selects zero or more of them.</summary>
public enum NotificationChannel
{
    Push,
    Email
}
