using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpatialVoice
{
    public enum NavigationTone { Direct, Technical, Casual, Encouraging }

    /// <summary>Half-open angle/distance bounds avoid duplicate bins. +180 is included
    /// in the final angular bin. Split rear-facing ranges into negative and positive entries.</summary>
    [Serializable]
    public struct NavigationPhrase
    {
        public string id, text;
        [Range(-180, 180)] public float minAngle, maxAngle;
        [Min(0)] public float minDistance, maxDistance;
        public NavigationTone tone;

        public bool Matches(float angle, float distance) => NavigationGeometry.Finite(angle) &&
            NavigationGeometry.Finite(distance) && angle >= minAngle &&
            (angle < maxAngle || (maxAngle == 180 && angle == 180)) &&
            distance >= minDistance && distance < maxDistance;
    }

    [CreateAssetMenu(fileName = "NavigationPhrases", menuName = "Study/Navigation Phrase Database")]
    public sealed class NavigationPhraseDatabase : ScriptableObject
    {
        public string version = "signed-navigation-v1";
        public NavigationPhrase[] phrases = Array.Empty<NavigationPhrase>();

        /// <summary>Reject malformed authoring before a phrase can become a spoken instruction.</summary>
        public void Validate()
        {
            if (phrases == null || phrases.Length == 0) throw new ArgumentException("Navigation catalog is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in phrases)
            {
                if (string.IsNullOrWhiteSpace(p.id) || !ids.Add(p.id) || string.IsNullOrWhiteSpace(p.text) ||
                    !NavigationGeometry.Finite(p.minAngle) || !NavigationGeometry.Finite(p.maxAngle) ||
                    p.minAngle < -180 || p.maxAngle > 180 || p.minAngle >= p.maxAngle ||
                    !NavigationGeometry.Finite(p.minDistance) || !NavigationGeometry.Finite(p.maxDistance) ||
                    p.minDistance < 0 || p.minDistance >= p.maxDistance || !Enum.IsDefined(typeof(NavigationTone), p.tone))
                    throw new ArgumentException("Invalid navigation phrase: " + p.id);
            }
        }
    }

    public readonly struct NavigationGeometry
    {
        public readonly float Angle, Distance;
        public NavigationGeometry(float angle, float distance) { Angle = angle; Distance = distance; }
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        /// <summary>0 means the supplied user's forward, not world north. Y is ignored for
        /// both vectors and distance. A vertical/zero direction has no meaningful azimuth.</summary>
        public static bool TryMeasure(Transform player, Transform target, out NavigationGeometry geometry)
        {
            geometry = default;
            return player != null && target != null && TryMeasure(player.position, player.forward, target.position, out geometry);
        }

        public static bool TryMeasure(Vector3 origin, Vector3 forward, Vector3 target, out NavigationGeometry geometry)
        {
            geometry = default;
            if (!Finite(origin) || !Finite(forward) || !Finite(target)) return false;
            origin.y = target.y = forward.y = 0;
            var delta = target - origin;
            if (delta.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f) return false;
            geometry = new NavigationGeometry(Vector3.SignedAngle(forward, delta, Vector3.up), Vector3.Distance(origin, target));
            return true;
        }

        /// <summary>O(n), no LINQ, no allocations. One candidate per tone, at most four.
        /// Caller owns the four-slot buffer. Never fills missing coverage with an invalid phrase.</summary>
        public static int Filter(NavigationPhrase[] phrases, NavigationGeometry geometry,
            NavigationPhrase[] results, int variation = 0, Predicate<NavigationPhrase> eligible = null)
        {
            if (results == null || results.Length < 4) throw new ArgumentException("Four candidate slots required.");
            if (phrases == null || phrases.Length == 0) return 0;
            int count = 0, tones = 0, start = (int)((uint)variation % (uint)phrases.Length);
            for (int i = 0; i < phrases.Length && count < 4; i++)
            {
                var phrase = phrases[(start + i) % phrases.Length];
                int mask = 1 << (int)phrase.tone;
                if ((tones & mask) != 0 || !phrase.Matches(geometry.Angle, geometry.Distance) ||
                    (eligible != null && !eligible(phrase))) continue;
                results[count++] = phrase; tones |= mask;
            }
            return count;
        }
    }
}
