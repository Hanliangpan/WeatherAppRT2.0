using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace WeatherAppRT2._0.Helpers
{
    public class GeoHelper
    {
        public static async Task<Geoposition> GetCurrentLocationAsync()
        {
            var geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = 1000,
                DesiredAccuracy = PositionAccuracy.Default
            };

            try
            {
                var position = await geolocator.GetGeopositionAsync(
                    maximumAge: TimeSpan.FromMinutes(10),
                    timeout: TimeSpan.FromSeconds(8));
                return position;
            }
            catch (UnauthorizedAccessException)
            {
                // 用户拒绝定位权限
                return null;
            }
            catch (Exception)
            {
                // 超时或其他错误
                return null;
            }
        }

        public static BasicGeoposition DefaultPosition
        {
            get
            {
                return new BasicGeoposition
                {
                    Latitude = AppConfig.DefaultLatitude,
                    Longitude = AppConfig.DefaultLongitude
                };
            }
        }
    }
}
