"""Observed horizontal wall visits from recorded gaze, not physiological fixations."""
import math
from dataclasses import dataclass, field

POLICY = "octagon_ray_wall_debounce_v1"
STABLE_SECONDS = 0.15  # Export policy for jitter suppression, not validated fixation duration.
MAX_GAP_SECONDS = 0.1


def vector(value):
    if not isinstance(value, dict):
        return None
    values = tuple(value.get(k) for k in "xyz")
    if any(type(v) not in (int, float) or not math.isfinite(v) for v in values):
        return None
    return values


def wall_for_ray(origin, direction, radius):
    """Intersect the fixed-origin octagon in XZ; pitch does not change the zone."""
    origin, direction = vector(origin), vector(direction)
    if origin is None or direction is None or radius is None:
        return None
    dx, _, dz = direction
    norm = math.sqrt(sum(v*v for v in direction))
    horizontal = math.hypot(dx, dz)
    if norm == 0 or horizontal / norm < 0.01:
        return None
    dx, dz = dx / horizontal, dz / horizontal
    hits = []
    half = radius * math.tan(math.pi / 8)
    for wall in range(8):
        theta = math.radians(45 * wall)
        nx, nz = math.sin(theta), math.cos(theta)
        offset = origin[0]*nx + origin[2]*nz
        if offset >= radius:
            return None  # Outside the enclosure: no unambiguous inside-to-wall visit.
        forward = dx*nx + dz*nz
        if forward <= 1e-8:
            continue
        distance = (radius-offset)/forward
        x, z = origin[0]+distance*dx, origin[2]+distance*dz
        if abs(x*nz-z*nx) <= half + 1e-8:
            hits.append((distance, wall))
    if not hits:
        return None
    hits.sort()
    if len(hits) > 1 and abs(hits[0][0]-hits[1][0]) < 1e-6:
        return None  # Exact corner belongs to neither wall for visit counting.
    return hits[0][1]


@dataclass
class ZoneVisits:
    radius: float | None = None
    cross_y: float | None = None
    layout: str | None = None
    candidate: int | None = None
    since: float | None = None
    last_time: float | None = None
    last_observation: tuple | None = None
    stream_identity: tuple | None = None
    current: int | None = None
    visited: set = field(default_factory=set)
    transitions: int = 0
    revisits: int = 0
    gaps: int = 0

    def capture_cross(self, record):
        # Ignore default JsonUtility zero vectors and transition records whose
        # readiness object can still describe the preceding trial.
        if record.get("kind") not in ("readiness_started", "readiness_completed"):
            return
        pos = vector(record.get("readinessPosition"))
        wall = record.get("readinessWall")
        if pos is None or type(wall) is not int or not 0 <= wall < 8:
            return
        nx, nz = math.sin(math.radians(45*wall)), math.cos(math.radians(45*wall))
        radius = pos[0]*nx + pos[2]*nz
        if radius <= 0 or abs(pos[0]*nz-pos[2]*nx) > 0.001:
            return
        self.radius, self.cross_y = radius, pos[1]

    def gap(self):
        if self.last_time is not None:
            self.gaps += 1
        self.candidate = self.since = self.last_time = self.current = None
        # Keep the last sensor identity: a repeated delivery cannot become a new sample.

    def observe(self, record):
        if self.layout != "rotational_scatter_v4" or self.radius is None:
            return
        if record.get("searchActive") is not True or record.get("gazeAvailable") is not True or record.get("gazeTracked") != 1:
            self.gap()
            return
        time = record["time"]
        sequence = record.get("gazeObservationSequence")
        has_sequence = type(sequence) is int and sequence >= 0
        identity = (record.get("gazeSource"), record.get("gazeRecordingId"), has_sequence)
        if self.stream_identity is not None and identity != self.stream_identity:
            self.gap()
            self.last_observation = None
        self.stream_identity = identity
        if has_sequence:
            observation = (identity, sequence)
            if observation == self.last_observation:
                if self.last_time is not None and time-self.last_time > MAX_GAP_SECONDS:
                    self.gap()
                return
            if self.last_observation is not None and sequence <= self.last_observation[1]:
                self.gap()
            self.last_observation = observation
        if self.last_time is not None and time-self.last_time > MAX_GAP_SECONDS + 1e-8:
            self.gap()
        zone = wall_for_ray(record.get("gazeOrigin"), record.get("gazeDirection"), self.radius)
        if zone is None:
            self.gap()
            return
        self.last_time = time
        if zone != self.candidate:
            self.candidate, self.since = zone, time
        if time-self.since < STABLE_SECONDS-1e-8 or zone == self.current:
            return
        if self.current is not None:
            self.transitions += 1
            self.revisits += int(zone in self.visited)
        self.visited.add(zone)
        self.current = zone

    def fields(self):
        available = bool(self.visited)
        return dict(
            zones_visited_count=len(self.visited) if available else None,
            unique_zones_visited_count=len(self.visited) if available else None,
            zone_transition_count=self.transitions if available else None,
            zone_revisit_count=self.revisits if available else None,
            zone_observation_gap_count=self.gaps,
            zone_measurement_status="observed_stable_gaze_walls" if available else
                "missing_recorded_cross_or_supported_layout" if self.radius is None or self.layout != "rotational_scatter_v4"
                else "no_stable_valid_gaze_zone",
            zone_measurement_method=POLICY,
            zone_stability_seconds=STABLE_SECONDS,
            zone_max_gap_seconds=MAX_GAP_SECONDS,
        )
