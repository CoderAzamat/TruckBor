namespace TruckBor.Domain.Enums;

public enum UserState
{
    None = 0,

    // Onboarding
    WaitingLanguage,
    WaitingRole,
    WaitingTerms,
    WaitingFullName,
    WaitingPhone,

    // Cargo post flow
    WaitingPostFrom,
    WaitingPostTo,
    WaitingPostCargoType,
    WaitingPostWeight,
    WaitingPostPrice,
    WaitingPostContact,

    // Transport post flow
    WaitingTransportFrom,
    WaitingTransportTo,
    WaitingTransportVehicle,
    WaitingTransportCapacity,
    WaitingTransportPrice,
    WaitingTransportPhone,

    // Dogruz post flow
    WaitingDogruzFrom,
    WaitingDogruzTo,
    WaitingDogruzCapacity,
    WaitingDogruzPrice,
    WaitingDogruzPhone,

    // Search
    WaitingSearchFrom,
    WaitingSearchTo,

    // Payment
    WaitingPaymentCheck,
    WaitingPaymentAmount,

    // Admin
    WaitingBroadcastText,
    WaitingCardNumber,
    WaitingCardHolder,
    WaitingCardBank,
    WaitingChannelId,
    WaitingSettingsValue,
    WaitingUserSearch,
    WaitingUserMessage,
    WaitingBalanceAmount,
    WaitingRejectReason,
    WaitingTariffName,
    WaitingTariffPrice,
    WaitingTariffDays,

    // AI post flow
    WaitingAiPostText,
    WaitingAiPostConfirm,

    // Accounts
    WaitingAccountPhone,
    WaitingAccountCode,
    WaitingVirtualNumberService,

    // Balance & Premium
    WaitingBalanceTopup,
    WaitingPremiumDuration,

    // Mini App OTP
    WaitingMiniAppOtp,

    // Virtual numbers
    WaitingVirtualCountry,
    WaitingVirtualService,

    // Admin video tutorial
    WaitingVideoTitle,
    WaitingVideoUrl,
    WaitingVideoServiceKey,
}
