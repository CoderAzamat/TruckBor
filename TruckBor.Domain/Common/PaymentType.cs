namespace TruckBor.Domain.Enums;

public enum PaymentType
{
    Manual = 0,        // Qo'lda (chek)
    Click = 1,         // Click.uz
    Payme = 2,         // Payme.uz
    Uzum = 3,          // Uzum Bank
    Stars = 4,         // Telegram Stars
    Paynet = 5,        // Paynet.uz
    Balance = 6,       // Ichki balansdan
    AdminGift = 99     // Admin tomonidan bepul
}