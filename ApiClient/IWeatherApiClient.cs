using System.Collections.Generic;
using System.Threading.Tasks;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.ApiClient
{
    public interface IWeatherApiClient
    {
        Task<WeatherData> GetFullWeatherByCityIdAsync(string cityId, string cityName);
        Task<WeatherData> GetFullWeatherByLocationAsync(double lat, double lon);
        Task<List<CitySearchResult>> SearchCityAsync(string keyword);
    }
}
