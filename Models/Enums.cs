namespace BarTasca.Models;

public enum TicketStatus
{
    Waiting,
    Notified,
    Confirmed,
    Skipped,
    Cancelled
}

public enum NotificationChannel
{
    SignalR,
    Sms
}

public enum NotificationType
{
    Reminder,
    Alert
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public enum StaffRole
{
    Admin,
    Worker
}