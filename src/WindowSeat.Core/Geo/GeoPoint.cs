namespace WindowSeat.Core.Geo;

/// <summary>Bod na Zemi: zeměpisná šířka a délka ve stupních (WGS84), nadmořská výška v metrech.</summary>
public readonly record struct GeoPoint(double LatitudeDegrees, double LongitudeDegrees, double AltitudeMeters = 0);
