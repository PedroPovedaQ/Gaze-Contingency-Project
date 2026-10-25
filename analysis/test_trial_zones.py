import math
import unittest
from trial_zones import ZoneVisits, wall_for_ray


def vec(x=0, y=0, z=0):
    return dict(x=x, y=y, z=z)


class ZoneTests(unittest.TestCase):
    def setUp(self):
        self.zones = ZoneVisits(layout="rotational_scatter_v4")
        self.zones.capture_cross(dict(kind="readiness_started", readinessWall=0,
                                     readinessPosition=vec(0, -.1, 1.5)))
        self.time = 0
        self.sequence = 0

    def test_v7_offset_cross_still_defines_the_same_wall_radius(self):
        self.zones = ZoneVisits(layout="rotational_scatter_v6_precue_theta")
        self.zones.capture_cross(dict(kind="readiness_completed", readinessWall=0,
                                     readinessPosition=vec(.1, -.1, 1.5)))
        self.assertEqual(self.zones.radius, 1.5)
        self.samples(0)
        self.assertEqual(self.zones.fields()["unique_zones_visited_count"], 1)

    def samples(self, wall, n=5, **overrides):
        angle = math.radians(wall*45)
        for _ in range(n):
            self.time += .05
            self.sequence += 1
            record = dict(time=self.time, searchSeconds=self.time, searchActive=True,
                          gazeAvailable=True, gazeTracked=1, gazeOrigin=vec(),
                          gazeDirection=vec(math.sin(angle), 0, math.cos(angle)),
                          gazeObservationSequence=self.sequence, gazeRecordingId="eyes")
            record.update(overrides)
            self.zones.observe(record)

    def test_unique_transitions_and_revisits(self):
        for zone in (0, 1, 2, 1, 0):
            self.samples(zone)
        fields = self.zones.fields()
        self.assertEqual(fields["unique_zones_visited_count"], 3)
        self.assertEqual(fields["zones_visited_count"], 3)
        self.assertEqual(fields["zone_transition_count"], 4)
        self.assertEqual(fields["zone_revisit_count"], 2)

    def test_jitter_and_vertical_scanning_do_not_invent_visits(self):
        self.samples(0)
        for i in range(20):
            self.samples((22.4 if i % 2 else 22.6)/45, n=1)
        self.samples(0, gazeDirection=vec(0, 10, 1))
        self.assertEqual(self.zones.fields()["unique_zones_visited_count"], 1)
        self.assertEqual(self.zones.transitions, 0)

    def test_invalid_pause_and_source_gaps_do_not_infer_transitions(self):
        self.samples(0)
        self.samples(1, n=1, gazeTracked=0)
        self.samples(1)
        self.samples(2, n=1, searchActive=False)
        self.samples(2)
        self.samples(3, gazeRecordingId="new-eyes")
        self.assertEqual(len(self.zones.visited), 4)
        self.assertEqual(self.zones.transitions, 0)
        self.assertEqual(self.zones.revisits, 0)
        self.assertEqual(self.zones.gaps, 3)

    def test_duplicates_cannot_accumulate_stability(self):
        self.samples(0, n=20, gazeObservationSequence=1)
        self.assertEqual(len(self.zones.visited), 0)

    def test_source_and_known_unknown_sequence_changes_break_stability(self):
        self.samples(0, n=3, gazeSource="source_a")
        self.samples(0, n=3, gazeSource="source_b")
        self.assertEqual(len(self.zones.visited), 0)
        self.samples(0, n=3, gazeSource="source_b", gazeObservationSequence=-1)
        self.assertEqual(len(self.zones.visited), 0)
        self.assertEqual(self.zones.gaps, 2)

    def test_delayed_samples_and_near_vertical_are_gaps(self):
        self.samples(0)
        self.time += .3
        self.samples(1)
        self.samples(1, gazeDirection=vec(0, 1, 0))
        self.assertEqual(self.zones.transitions, 0)
        self.assertEqual(self.zones.gaps, 2)

    def test_wall_intersection_accounts_for_eye_position_and_corners(self):
        self.assertEqual(wall_for_ray(vec(.8, 0, 0), vec(0, 0, 1), 1.5), 1)
        angle = math.radians(22.5)
        self.assertIsNone(wall_for_ray(vec(), vec(math.sin(angle), 0, math.cos(angle)), 1.5))
        self.assertIsNone(wall_for_ray(vec(10, 0, 0), vec(0, 0, 1), 1.5))

    def test_absent_geometry_is_not_zero_visits(self):
        zones = ZoneVisits()
        self.assertIsNone(zones.fields()["zones_visited_count"])


if __name__ == "__main__":
    unittest.main()
