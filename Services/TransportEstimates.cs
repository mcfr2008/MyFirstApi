using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Shared by the carbon footprint and the route planner.
public static class TransportEstimates
{
    // Great-circle distance between two located points, adjusted for real routes,
    // or null when either point has no coordinates. Sea and air follow the GLEC
    // Framework (x1.15, +95 km); road and rail use a typical x1.2 detour factor.
    public static decimal? EstimateKm(TransportMode mode, Location from, Location to)
    {
        if (from.Latitude is not { } lat1 || from.Longitude is not { } lon1 ||
            to.Latitude is not { } lat2 || to.Longitude is not { } lon2)
        {
            return null;
        }

        var km = (decimal)GreatCircleKm((double)lat1, (double)lon1, (double)lat2, (double)lon2);
        return mode switch
        {
            TransportMode.Sea => km * 1.15m,
            TransportMode.Air => km + 95m,
            _ => km * 1.2m
        };
    }

    // The carrier's own factor for the mode, else the mode default (active factors only).
    public static EmissionFactor? PickFactor(IEnumerable<EmissionFactor> activeFactors, TransportMode mode, int? carrierId)
    {
        var forMode = activeFactors.Where(f => f.Mode == mode).ToList();
        return forMode.FirstOrDefault(f => carrierId != null && f.CarrierId == carrierId)
               ?? forMode.FirstOrDefault(f => f.CarrierId == null);
    }

    // Haversine distance on a sphere of mean Earth radius.
    private static double GreatCircleKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        static double Rad(double degrees) => degrees * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(a));
    }
}
