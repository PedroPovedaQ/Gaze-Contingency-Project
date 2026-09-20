using System;
using UnityEngine;

// Public fields deliberately match the portable JSONL schema (Unity JsonUtility).
[Serializable]
public sealed class TrialReplayRecord
{
    public int schema = 1;
    public string kind;
    public long sequence;
    public double time;
    public int frame;
    public string recordingId, sourceType, appVersion, buildGuid, unityVersion, utc;
    public string participantCode, sourceRunFolder, sourceTrialId;
    public int runNumber;
    public string trialId, voice, detail, objectId, clip, text;
    public int round;
    public bool practice, correct, searchActive, visible, complete;
    public double searchSeconds;
    public Vector3 origin, headPosition, gazeOrigin, gazeDirection, xrOriginPosition;
    public Quaternion originRotation = Quaternion.identity;
    public Quaternion headRotation = Quaternion.identity;
    public Quaternion xrOriginRotation = Quaternion.identity;
    public bool headAvailable, gazeAvailable, xrOriginAvailable;
    // -1 = unknown/not exposed; 0 = explicitly untracked; 1 = explicitly tracked.
    public int headTracked = -1, gazeTracked = -1;
    public float dwell, audioVolume = 0.7f;
    public string hoveredId, targetId, layout;
    public TrialReplayObject[] objects;
    public TrialReplayMesh[] meshes;
    public TrialReplayTransform[] changes;
}

[Serializable]
public sealed class TrialReplayObject
{
    public string id, shape, color, mesh;
    public Color rgba;
    public bool target;
    public Vector3 position, scale;
    public Quaternion rotation;
}

[Serializable]
public sealed class TrialReplayTransform
{
    public string id;
    public Vector3 position, scale;
    public Quaternion rotation;
}

[Serializable]
public sealed class TrialReplayMesh
{
    public string id;
    public Vector3[] vertices, normals;
    public int[] triangles;
}
