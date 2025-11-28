namespace BinomoBackend.Domain.Enums;

public enum LimitOrderStatus
{
    Pending,    // Ожидает исполнения
    Executed,   // Исполнен
    Cancelled,  // Отменен
    Expired     // Истек
}