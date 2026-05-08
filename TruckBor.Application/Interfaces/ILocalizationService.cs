using TruckBor.Domain.Enums;

namespace TruckBor.Application.Interfaces;

public interface ILocalizationService
{
    string Get(string key, Language language);
    string Get(string key, Language language, params object[] args);
}