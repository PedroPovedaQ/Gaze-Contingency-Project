// Diagnostic interception only: never generates poses or changes the application's gaze ray.
#include <openxr/openxr.h>
#include <array>
#include <atomic>
#include <chrono>
#include <cstring>
#include <mutex>
#include <unordered_map>
#include <unordered_set>
#include <cstdint>

#define EXPORT extern "C" __attribute__((visibility("default")))
struct GazeRecord {
    uint64_t sequence, epoch, session, space, baseSpace, locationFlags;
    int64_t requestedTime, runtimePoseTime, receiptMonotonicNs;
    int32_t result, timestampChained;
    float qx,qy,qz,qw,px,py,pz;
    float offsetQx,offsetQy,offsetQz,offsetQw,offsetX,offsetY,offsetZ;
};
struct GazeStatus {
    uint64_t epoch, records, dropped, spaces, eyeActions, locateCalls;
    int32_t enabled, recordBytes;
};
namespace {
std::atomic<PFN_xrGetInstanceProcAddr> nextGet{nullptr};
#define FN(name) std::atomic<PFN_##name> real_##name{nullptr};
FN(xrSuggestInteractionProfileBindings) FN(xrCreateActionSpace) FN(xrLocateSpace)
FN(xrCreateAction) FN(xrDestroyAction) FN(xrDestroyActionSet)
FN(xrDestroySpace) FN(xrDestroySession)
#undef FN
struct SpaceInfo { XrAction action; XrSession session; XrPosef offset; };
std::mutex gate;
std::atomic<bool> enabled{false};
XrPath eyeProfile{}, eyeBinding{};
std::unordered_set<XrAction> eyeActions;
std::unordered_map<XrSpace,SpaceInfo> spaces;
std::unordered_map<XrAction,XrActionSet> actions;
constexpr size_t capacity=8192;
std::array<GazeRecord,capacity> ring{};
size_t head{}, count{};
uint64_t epoch{}, sequence{}, dropped{}, locateCalls{};
template<class T> uint64_t id(T value) { return static_cast<uint64_t>(reinterpret_cast<uintptr_t>(value)); }
int64_t monotonic() { return std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now().time_since_epoch()).count(); }
void eraseAction(XrAction action) {
    eyeActions.erase(action); actions.erase(action);
    for(auto it=spaces.begin();it!=spaces.end();) {
        if(it->second.action==action) it=spaces.erase(it); else ++it;
    }
}
XrResult XRAPI_CALL suggest(XrInstance instance,const XrInteractionProfileSuggestedBinding* info) {
    auto result=real_xrSuggestInteractionProfileBindings.load()(instance,info);
    if(enabled && XR_SUCCEEDED(result) && info) {
        std::lock_guard<std::mutex> lock(gate);
        if(info->interactionProfile==eyeProfile) {
            eyeActions.clear(); // A successful re-suggestion replaces this profile's bindings.
            for(uint32_t i=0;i<info->countSuggestedBindings;++i)
                if(info->suggestedBindings[i].binding==eyeBinding) eyeActions.insert(info->suggestedBindings[i].action);
        }
    }
    return result;
}
XrResult XRAPI_CALL createAction(XrActionSet set,const XrActionCreateInfo* info,XrAction* action) {
    auto result=real_xrCreateAction.load()(set,info,action);
    if(enabled && XR_SUCCEEDED(result) && action) { std::lock_guard<std::mutex> lock(gate); actions[*action]=set; }
    return result;
}
XrResult XRAPI_CALL createSpace(XrSession session,const XrActionSpaceCreateInfo* info,XrSpace* space) {
    auto result=real_xrCreateActionSpace.load()(session,info,space);
    if(enabled && XR_SUCCEEDED(result) && info && space) {
        std::lock_guard<std::mutex> lock(gate); spaces[*space]={info->action,session,info->poseInActionSpace};
    }
    return result;
}
XrResult XRAPI_CALL locate(XrSpace space,XrSpace base,XrTime time,XrSpaceLocation* location) {
    SpaceInfo info{}; uint64_t capturedEpoch{}; bool eye=false;
    if(enabled && location) {
        std::lock_guard<std::mutex> lock(gate); ++locateCalls;
        auto it=spaces.find(space);
        if(it!=spaces.end() && eyeActions.count(it->second.action)) { eye=true; info=it->second; capturedEpoch=epoch; }
    }
    if(!eye) return real_xrLocateSpace.load()(space,base,time,location);
    // Suggested binding identifies the candidate action, not a verified physical sensor source.
    XrEyeGazeSampleTimeEXT extra{XR_TYPE_EYE_GAZE_SAMPLE_TIME_EXT,nullptr,0};
    auto originalNext=location->next;
    XrEyeGazeSampleTimeEXT* timestamp=nullptr;
    auto node=static_cast<XrBaseOutStructure*>(location->next);
    for(int i=0;node && i<64;++i,node=node->next)
        if(node->type==XR_TYPE_EYE_GAZE_SAMPLE_TIME_EXT) { timestamp=reinterpret_cast<XrEyeGazeSampleTimeEXT*>(node); break; }
    if(!timestamp) { extra.next=originalNext; location->next=&extra; timestamp=&extra; }
    timestamp->time=0; // A runtime that leaves the field untouched must not reuse an earlier value.
    auto result=real_xrLocateSpace.load()(space,base,time,location);
    location->next=originalNext;
    GazeRecord record{};
    record.epoch=capturedEpoch; record.session=id(info.session); record.space=id(space); record.baseSpace=id(base);
    record.requestedTime=time; record.result=result; record.timestampChained=1;
    record.receiptMonotonicNs=monotonic();
    // Output pose/timestamps are unspecified on failure. Keep result, but do not copy stale values.
    if(XR_SUCCEEDED(result)) {
        record.locationFlags=location->locationFlags; record.runtimePoseTime=timestamp->time;
        auto p=location->pose; record.qx=p.orientation.x;record.qy=p.orientation.y;record.qz=p.orientation.z;record.qw=p.orientation.w;
        record.px=p.position.x;record.py=p.position.y;record.pz=p.position.z;
    }
    auto p=info.offset;record.offsetQx=p.orientation.x;record.offsetQy=p.orientation.y;record.offsetQz=p.orientation.z;record.offsetQw=p.orientation.w;
    record.offsetX=p.position.x;record.offsetY=p.position.y;record.offsetZ=p.position.z;
    { std::lock_guard<std::mutex> lock(gate);
      if(enabled && epoch==capturedEpoch) {
        record.sequence=++sequence;
        if(count==capacity) { head=(head+1)%capacity;--count;++dropped; }
        ring[(head+count)%capacity]=record;++count;
      }
    }
    return result;
}
XrResult XRAPI_CALL destroySpace(XrSpace space) {
    auto result=real_xrDestroySpace.load()(space); if(XR_SUCCEEDED(result)) { std::lock_guard<std::mutex> lock(gate); spaces.erase(space); } return result;
}
XrResult XRAPI_CALL destroyAction(XrAction action) {
    auto result=real_xrDestroyAction.load()(action); if(XR_SUCCEEDED(result)) { std::lock_guard<std::mutex> lock(gate); eraseAction(action); } return result;
}
XrResult XRAPI_CALL destroySet(XrActionSet set) {
    auto result=real_xrDestroyActionSet.load()(set);
    if(XR_SUCCEEDED(result)) { std::lock_guard<std::mutex> lock(gate);
        for(auto it=actions.begin();it!=actions.end();) { if(it->second==set) { auto action=it->first; ++it; eraseAction(action); } else ++it; }
    } return result;
}
XrResult XRAPI_CALL destroySession(XrSession session) {
    auto result=real_xrDestroySession.load()(session);
    if(XR_SUCCEEDED(result)) { std::lock_guard<std::mutex> lock(gate);
        for(auto it=spaces.begin();it!=spaces.end();) { if(it->second.session==session) it=spaces.erase(it); else ++it; }
    } return result;
}
XrResult XRAPI_CALL getProc(XrInstance instance,const char* name,PFN_xrVoidFunction* fn) {
    auto result=nextGet.load()(instance,name,fn);
    if(XR_FAILED(result) || !fn || !*fn) return result;
#define HOOK(api,wrapper) if(std::strcmp(name,#api)==0) { real_##api=reinterpret_cast<PFN_##api>(*fn);*fn=reinterpret_cast<PFN_xrVoidFunction>(wrapper);return result; }
    HOOK(xrSuggestInteractionProfileBindings,suggest) HOOK(xrCreateActionSpace,createSpace) HOOK(xrLocateSpace,locate)
    HOOK(xrCreateAction,createAction) HOOK(xrDestroyAction,destroyAction) HOOK(xrDestroyActionSet,destroySet)
    HOOK(xrDestroySpace,destroySpace) HOOK(xrDestroySession,destroySession)
#undef HOOK
    return result;
}
}
EXPORT PFN_xrGetInstanceProcAddr ng_hook(PFN_xrGetInstanceProcAddr next) { nextGet=next;return getProc; }
EXPORT int ng_begin(uint64_t instanceValue,int extensionEnabled) {
    enabled=false;
    { std::lock_guard<std::mutex> lock(gate);
      ++epoch;sequence=dropped=locateCalls=0;head=count=0;spaces.clear();actions.clear();eyeActions.clear();eyeProfile=eyeBinding=XR_NULL_PATH;
    }
    if(!extensionEnabled || !nextGet) return 0;
    auto instance=reinterpret_cast<XrInstance>(static_cast<uintptr_t>(instanceValue));
    PFN_xrStringToPath stringToPath=nullptr;
    if(XR_FAILED(nextGet.load()(instance,"xrStringToPath",reinterpret_cast<PFN_xrVoidFunction*>(&stringToPath))) || !stringToPath) return 0;
    XrPath profile{},binding{};
    if(XR_FAILED(stringToPath(instance,"/interaction_profiles/ext/eye_gaze_interaction",&profile)) ||
       XR_FAILED(stringToPath(instance,"/user/eyes_ext/input/gaze_ext/pose",&binding))) return 0;
    { std::lock_guard<std::mutex> lock(gate); eyeProfile=profile;eyeBinding=binding; }
    enabled=true;return 1;
}
EXPORT void ng_end() { enabled=false;std::lock_guard<std::mutex> lock(gate);spaces.clear();actions.clear();eyeActions.clear(); }
EXPORT int ng_drain(GazeRecord* output,int maxRecords) {
    if(!output || maxRecords<=0) return 0;
    std::lock_guard<std::mutex> lock(gate);int n=0;
    while(count && n<maxRecords) { output[n++]=ring[head];head=(head+1)%capacity;--count; } return n;
}
EXPORT void ng_status(GazeStatus* status) {
    if(!status)return;std::lock_guard<std::mutex> lock(gate);
    *status={epoch,sequence,dropped,spaces.size(),eyeActions.size(),locateCalls,enabled?1:0,static_cast<int32_t>(sizeof(GazeRecord))};
}
EXPORT int64_t ng_monotonic_ns() { return monotonic(); }
