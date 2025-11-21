namespace BinomoBackend.Domain.Enums;

public enum PositionStatus
{
    Pending = 1,    // For limit orders
    Open = 2,       // For active positions
    Closed = 3,     // Close
    Cancelled = 4   // Cancelled
}