import buffer from '@turf/buffer';
import booleanPointInPolygon from '@turf/boolean-point-in-polygon';
import nearestPointOnLine from '@turf/nearest-point-on-line';
import { lineString, point } from '@turf/helpers';

export const ARAD_CENTER = [46.1866, 21.3123];

const OSRM_BASE = 'https://router.project-osrm.org/route/v1/driving';
const NOMINATIM = 'https://nominatim.openstreetmap.org/search';

/** @returns {Promise<[number, number] | null>} [lat, lon] */
export async function geocodeCity(city) {
  const url = `${NOMINATIM}?format=json&limit=1&countrycodes=ro&q=${encodeURIComponent(city + ', Romania')}`;
  const res = await fetch(url, { headers: { 'Accept-Language': 'en' } });
  if (!res.ok) return null;
  const data = await res.json();
  return data.length ? [parseFloat(data[0].lat), parseFloat(data[0].lon)] : null;
}

/**
 * Driving route between two [lat, lon] points.
 * @returns {Promise<{geometry: object, distanceKm: number, durationMin: number} | null>}
 */
export async function fetchRoute(origin, dest) {
  const url = `${OSRM_BASE}/${origin[1]},${origin[0]};${dest[1]},${dest[0]}?overview=full&geometries=geojson`;
  const res = await fetch(url);
  if (!res.ok) return null;
  const data = await res.json();
  const route = data.routes?.[0];
  if (!route) return null;
  return {
    geometry: route.geometry,
    distanceKm: route.distance / 1000,
    durationMin: Math.round(route.duration / 60),
  };
}

/**
 * Finds the referees that live within `bufferKm` of the route line.
 * @returns {{buffer: object, onRouteIds: Set<string>, offRouteKm: Record<string, string>}}
 */
export function computeCoverage(geometry, referees, bufferKm) {
  const line = lineString(geometry.coordinates);
  const zone = buffer(line, bufferKm, { units: 'kilometers' });
  const onRouteIds = new Set();
  const offRouteKm = {};

  for (const r of referees) {
    if (r.lat == null || r.lon == null) continue;
    const pt = point([r.lon, r.lat]);
    if (booleanPointInPolygon(pt, zone)) {
      onRouteIds.add(r.id);
      offRouteKm[r.id] = nearestPointOnLine(line, pt, { units: 'kilometers' }).properties.dist.toFixed(1);
    }
  }
  return { buffer: zone, onRouteIds, offRouteKm };
}
