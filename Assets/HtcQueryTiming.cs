using System;
using System.Runtime.InteropServices;

[Serializable, StructLayout(LayoutKind.Sequential)]
public struct HtcQueryStamp
{
    public ulong epoch, tracker, baseSpace, session;
    public long requestedTime, returnedTime, beforeNs, afterNs;
    public int result, observed;
    public ulong headFlags;
    public float headQx,headQy,headQz,headQw,headX,headY,headZ;
}

[Serializable]
public sealed class HtcQueryTiming
{
    public string semantics = "gaze=requested_pose_time;pupil/geometric=runtime_reported_capture_time;XrTime_ns";
    public bool sensorClockValidated = false;
    public HtcQueryStamp gaze, pupil, geometric;
}
