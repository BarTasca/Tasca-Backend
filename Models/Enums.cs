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
    Sms,
    WebPush
}

public enum NotificationType
{
    Reminder = 1,
    Turn = 2,
    Manual = 3
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